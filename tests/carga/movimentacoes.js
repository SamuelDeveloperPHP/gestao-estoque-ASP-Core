// Teste de carga (k6) da API de estoque.
//
// Rodar contra o ambiente do compose (a partir da raiz do repositório):
//   Get-Content tests/carga/movimentacoes.js | docker run -i --rm --network gestao-estoque_default `
//     -e BASE_URL=http://api:8080 -e ADMIN_EMAIL=... -e ADMIN_PASSWORD=... grafana/k6:0.54.0 run -
//
// Cenários:
//  - espalhada: cada VU trabalha em um produto diferente (caso típico: pouca disputa por linha).
//  - disputa: todos os VUs mexem no MESMO saldo (pior caso: concorrência otimista + retries).
import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const PRODUTOS = 20;

// 200/201 esperados; 422 (regra de negócio) NÃO é esperado neste teste e conta como falha.
http.setResponseCallback(http.expectedStatuses(200, 201));
const erros5xx = new Rate('erros_5xx');

export const options = {
  scenarios: {
    espalhada: {
      executor: 'ramping-vus', exec: 'espalhada', startVUs: 1,
      stages: [{ duration: '10s', target: 20 }, { duration: '30s', target: 20 }, { duration: '5s', target: 0 }],
    },
    disputa: {
      executor: 'constant-vus', exec: 'disputa', vus: 10, duration: '20s', startTime: '50s',
    },
  },
  thresholds: {
    'http_req_duration{scenario:espalhada}': ['p(95)<500'],
    'http_req_failed{scenario:espalhada}': ['rate<0.01'],
    'erros_5xx': ['rate<0.01'],
  },
};

function jsonH(token, chave) {
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
  if (chave) headers['Idempotency-Key'] = chave;
  return { headers };
}

function post(token, url, corpo, chave) {
  return http.post(`${BASE}${url}`, JSON.stringify(corpo), jsonH(token, chave));
}

export function setup() {
  const login = http.post(`${BASE}/api/v1/auth/login`,
    JSON.stringify({ email: __ENV.ADMIN_EMAIL, senha: __ENV.ADMIN_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } });
  if (login.status !== 200) throw new Error(`login falhou: ${login.status}`);
  const token = login.json('accessToken');

  const sufixo = Date.now().toString(36);
  const dep = post(token, '/api/v1/depositos', { codigo: `K6${sufixo}`.slice(0, 20), nome: 'Depósito de carga' });
  const depositoId = dep.json('id');

  const produtos = [];
  for (let i = 0; i <= PRODUTOS; i++) { // índice PRODUTOS = produto "quente" do cenário de disputa
    const p = post(token, '/api/v1/produtos', { sku: `K6-${sufixo}-${i}`, nome: `Produto de carga ${i}` });
    const id = p.json('id');
    // Saldo inicial alto para que as saídas nunca faltem.
    post(token, '/api/v1/estoque/movimentacoes',
      { produtoId: id, depositoId, tipo: 'Entrada', quantidade: 1000000 }, `seed-${sufixo}-${i}`);
    produtos.push(id);
  }
  return { token, depositoId, produtos };
}

function movimentar(d, produtoId) {
  const chave = `${__VU}-${__ITER}-${Date.now()}-${Math.random()}`;
  const tipo = Math.random() < 0.7 ? 'Saida' : 'Entrada';
  const r = post(d.token, '/api/v1/estoque/movimentacoes',
    { produtoId, depositoId: d.depositoId, tipo, quantidade: 1 }, chave);
  erros5xx.add(r.status >= 500);
  check(r, { 'movimentação 201': (x) => x.status === 201 });
}

export function espalhada(d) {
  const produtoId = d.produtos[__VU % PRODUTOS];
  if (Math.random() < 0.8) {
    movimentar(d, produtoId);
  } else {
    const r = http.get(`${BASE}/api/v1/estoque/saldos?produtoId=${produtoId}`, jsonH(d.token));
    erros5xx.add(r.status >= 500);
    check(r, { 'consulta 200': (x) => x.status === 200 });
  }
}

export function disputa(d) {
  movimentar(d, d.produtos[PRODUTOS]);
}
