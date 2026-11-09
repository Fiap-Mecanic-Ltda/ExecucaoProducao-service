# ExecucaoProducao-service

Microsserviço de **Execução e Produção** da MechanicLtda: fila de execução das ordens de serviço,
diagnóstico, controle de estoque (peças e insumos) e catálogo de serviços da oficina. É um
**participante da Saga** orquestrada pelo OS Service: recebe os comandos de diagnóstico e de
execução e comunica a finalização.

| Documento | Conteúdo |
|---|---|
| [Contratos de mensagens](docs/contratos-eventos.md) | Comandos, eventos e status da Saga (cópia idêntica à do OS Service) |
| [ADR-001](docs/adr/ADR-001-mensageria-rabbitmq.md) | Mensageria com RabbitMQ e MassTransit |
| [ADR-002](docs/adr/ADR-002-saga-orquestrada.md) | Saga orquestrada pelo OS Service |
| [ADR-003](docs/adr/ADR-003-dynamodb.md) | DynamoDB como banco NoSQL da Execução |
| [ADR-004](docs/adr/ADR-004-divisao-execucao.md) | O que sai do MechanicLtda para a Execução |

Repositórios relacionados: `ExecucaoProducao-InfraKubernete` (deploy no k3s) e
`ExecucaoProducao-InfraSGBD` (tabelas DynamoDB).

## Estrutura

Mesmas camadas do MechanicLtda e do OS Service:

```text
ExecucaoProducao-service/
├── src/
│   ├── ExecucaoService.API             # Host REST: controllers, Swagger, JWT, CorrelationId, /health
│   ├── ExecucaoService.Bootstrap       # Composition root: DynamoDB, logs, roles
│   ├── ExecucaoService.Application     # Casos de uso e segurança
│   ├── ExecucaoService.Domain          # Entidades, enums e regras (status da execução)
│   └── ExecucaoService.Infrastructure  # DynamoDB (tabelas execucao-*) e integrações
├── tests/
│   ├── ExecucaoService.UnitTests       # Domínio, aplicação e bootstrap (xUnit + Moq)
│   └── ExecucaoService.API.Tests       # API em memória (WebApplicationFactory)
├── docs/                               # Contratos da Saga e ADRs
├── docker-compose.yml                  # DynamoDB Local + RabbitMQ + API
└── .github/workflows/ci.yml            # Build, testes e build da imagem
```

## Rodando localmente

Pré-requisitos: .NET 9 SDK e Docker.

```bash
cp .env.example .env            # ajuste a senha do RabbitMQ e a chave JWT
docker compose up -d --build
curl http://localhost:8082/health
```

| Serviço | Endereço |
|---|---|
| API e Swagger | http://localhost:8082/swagger |
| Painel do RabbitMQ | http://localhost:15673 (usuário e senha do `.env`) |
| DynamoDB Local | http://localhost:8000 |

As portas não conflitam com o docker-compose do OS Service, então os dois sobem juntos. Para a
API aceitar os tokens do resto do sistema, `JWT_SECRET_KEY` precisa ser a mesma chave do
MechanicLtda e da Lambda de autenticação.

Rodando sem Docker (com o DynamoDB Local do compose no ar):

```bash
dotnet user-secrets set JWT_SECRET_KEY "<chave>" --project src/ExecucaoService.API
DynamoDb__ServiceUrl=http://localhost:8000 AWS_ACCESS_KEY_ID=local AWS_SECRET_ACCESS_KEY=local \
  dotnet run --project src/ExecucaoService.API     # http://localhost:5080/swagger
```

## Testes

```bash
dotnet test ExecucaoService.sln --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```
