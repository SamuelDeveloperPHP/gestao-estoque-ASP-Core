# gestao-estoque-ASP-Core

Monolito modular em ASP.NET Core 8 para gestão de estoque, com PostgreSQL.

## Estrutura

```
src/
  BuildingBlocks/        Entity, eventos de domínio, DomainException
  Modules/
    Identidade/          usuários e perfis (JWT/OIDC: próxima etapa)
    Catalogo/            produtos e SKU
    Estoque/             saldos e movimentações (schema "estoque")
  Api/                   host REST (/api/v1), health checks, Swagger
  Web/                   UI MVC com tema Velzon; consome a API (login por cookie + JWT)
tests/
  Estoque.Domain.Tests   regras de domínio
  Estoque.Arch.Tests     módulos não podem depender uns dos outros
deploy/k8s/              Deployment, Service, HPA e PDB
```

Regras: cada módulo tem seu schema no PostgreSQL e só depende do BuildingBlocks.
Integração entre módulos: eventos de domínio (futuro: Outbox + RabbitMQ).

## Pré-requisitos

SDK do .NET 8, Docker, Git. IDE: Visual Studio 2022, Rider ou VS Code (C# Dev Kit).

## Tema Velzon (Web)

O Velzon é um tema comercial e **não é versionado** (`src/Web/wwwroot/assets` está no `.gitignore`).
Com a sua licença, copie os arquivos necessários:

```powershell
./tools/copiar-assets-velzon.ps1 -Origem "C:\caminho\do\projeto\Velzon"
```

Sem os assets o Web funciona, mas sem estilo.

## Rodando

```bash
cp .env.example .env   # preencha os valores
docker compose up --build
```

- UI: http://localhost:8081 (entre com `ADMIN_EMAIL` / `ADMIN_PASSWORD` do `.env`)
- API + Swagger: http://localhost:8080/swagger
- Traces (Jaeger): http://localhost:16686 · RabbitMQ: http://localhost:15672 (usuário `estoque`, senha `RABBITMQ_PASSWORD`)

Testes: `dotnet test`

## API (Estoque)

- `POST /api/v1/estoque/movimentacoes` — cabeçalho `Idempotency-Key` obrigatório.
  Corpo: `{ "produtoId": "...", "depositoId": "...", "tipo": "Entrada|Saida", "quantidade": 10 }`.
  `201` na primeira vez, `200` ao repetir a mesma chave, `422` para regra de domínio
  (saldo insuficiente, chave reutilizada com outro conteúdo), `400` sem a chave.
- `GET /api/v1/estoque/saldos?produtoId=&depositoId=&skip=&take=`

Cada movimentação grava o ledger (`estoque.movimentacoes`, só inserções) e atualiza o saldo
na mesma transação. Concorrência: lock otimista (`xmin`) com retry, mais o check `Quantidade >= 0` no banco.

## Migrations

Local: `Database__MigrateOnStartup=true` (já no compose). Em produção, rode num Job do Kubernetes.
Nova migration (precisa do `dotnet-ef`):

```bash
dotnet ef migrations add NomeDaMigration -p src/Modules/Estoque -s src/Api -o Infrastructure/Migrations
```

## Mensageria e observabilidade

- **Outbox:** cada movimentação grava um evento em `estoque.outbox` na mesma transação; um serviço em segundo plano
  publica em `estoque.eventos` (RabbitMQ, topic, com confirmação). Entrega "pelo menos uma vez": consumidores
  deduplicam por `MessageId`. Sem `RabbitMq__Host` o Outbox apenas acumula. A API traz um consumidor de exemplo
  (`api.estoque.auditoria`) que registra os eventos em log.
- **OpenTelemetry:** traces de ASP.NET, HttpClient e Npgsql no Web e na API, exportados por OTLP
  (`OTEL_EXPORTER_OTLP_ENDPOINT`). O contexto é propagado do Web para a API.

## Kubernetes

`deploy/k8s`: `api.yaml`, `web.yaml` (PVC das chaves de cookie), `migrate-job.yaml` e `kustomize`.
Os Secrets esperados estão em `secrets.exemplo.yaml` (valores de mentira). Ordem de implantação:

```bash
kubectl apply -f deploy/k8s/migrate-job.yaml
kubectl wait --for=condition=complete job/gestao-estoque-migrate
kubectl apply -k deploy/k8s
```

O Job roda a imagem da API com `--migrate-only` (aplica migrations, cria o admin inicial e encerra).

## Teste de carga

```powershell
Get-Content tests/carga/movimentacoes.js | docker run -i --rm --network gestao-estoque_default `
  -e BASE_URL=http://api:8080 -e ADMIN_EMAIL=... -e ADMIN_PASSWORD=... grafana/k6:0.54.0 run -
```

Medição local (Docker Desktop, 1 réplica, 30 VUs, 38 mil requisições): 0 erros, ~540 req/s, p95 57 ms,
inclusive com 10 VUs disputando o mesmo saldo. O saldo é ajustado por `UPDATE ... WHERE saldo + delta >= 0`
(atômico), então a disputa enfileira na trava da linha em vez de gerar conflitos. Números locais, só como referência.

## Próximas etapas

1. Limpeza periódica do Outbox (mensagens processadas) e dead-letter para eventos que falham sempre.
2. Métricas (OpenTelemetry/Prometheus) e dashboards; propagar o trace também pelo Outbox.
3. Transferência entre depósitos, reservas e relatórios (curva ABC, ponto de reposição).
4. Troca de senha, bloqueio de conta e login via OIDC (Keycloak/Entra ID).
5. Teste de carga em ambiente dedicado, com réplicas e PostgreSQL separado.
