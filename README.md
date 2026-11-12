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

## Próximas etapas

1. Migrations do EF Core por módulo e endpoints de movimentação (com `Idempotency-Key`).
2. Autenticação JWT/OIDC no módulo Identidade.
3. Outbox + mensageria; OpenTelemetry; UI (template Velzon) consumindo a API.
