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
  Api/                   host REST (/api/v1), health checks
tests/
  Estoque.Domain.Tests   regras de domínio
  Estoque.Arch.Tests     módulos não podem depender uns dos outros
deploy/k8s/              Deployment, Service, HPA e PDB
```

Regras: cada módulo tem seu schema no PostgreSQL e só depende do BuildingBlocks.
Integração entre módulos: eventos de domínio (futuro: Outbox + RabbitMQ).

## Pré-requisitos

SDK do .NET 8, Docker, Git. IDE: Visual Studio 2022, Rider ou VS Code (C# Dev Kit).

## Rodando

```bash
cp .env.example .env   # defina POSTGRES_PASSWORD
docker compose up --build
curl http://localhost:8080/health/ready
```

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

## Próximas etapas

1. Autenticação JWT/OIDC no módulo Identidade e `[Authorize]` nos endpoints (hoje abertos).
2. Persistência do Catálogo (validar `produtoId`) e testes de integração com Testcontainers no CI.
3. Outbox + mensageria; OpenTelemetry; UI (template Velzon) consumindo a API.
