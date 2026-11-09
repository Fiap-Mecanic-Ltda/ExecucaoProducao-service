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
│   ├── ExecucaoService.Bootstrap       # Composition root: DynamoDB, MassTransit, logs, roles
│   ├── ExecucaoService.Contratos       # Mensagens da Saga (cópia do OsService.Contratos)
│   ├── ExecucaoService.Application     # Casos de uso e segurança
│   ├── ExecucaoService.Domain          # Entidades, enums e regras (status da execução)
│   └── ExecucaoService.Infrastructure  # DynamoDB (tabelas execucao-*) e integrações
├── tests/
│   ├── Compartilhado                   # Repositórios em memória usados pelos testes
│   ├── ExecucaoService.UnitTests       # Domínio, aplicação, consumidores (test harness do MassTransit) e bootstrap
│   ├── ExecucaoService.API.Tests       # API em memória (WebApplicationFactory)
│   └── ExecucaoService.IntegrationTests # Repositórios contra o DynamoDB Local
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

No compose, `DynamoDb__CriarTabelas=true` faz a API criar as tabelas que faltam ao subir. Na AWS
as tabelas vêm do Terraform do `ExecucaoProducao-InfraSGBD` e essa opção fica desligada.

Rodando sem Docker (com o DynamoDB Local do compose no ar):

```bash
dotnet user-secrets set JWT_SECRET_KEY "<chave>" --project src/ExecucaoService.API
DynamoDb__ServiceUrl=http://localhost:8000 DynamoDb__CriarTabelas=true AWS_ACCESS_KEY_ID=local AWS_SECRET_ACCESS_KEY=local \
  dotnet run --project src/ExecucaoService.API     # http://localhost:5080/swagger
```

## Rotas

Todas exigem o JWT do MechanicLtda com papel `Administrador` ou `Funcionario`.

| Rota | O que faz |
|---|---|
| `GET /api/estoque`, `GET /api/estoque/tipo/{Peca\|Insumo}`, `GET /api/estoque/{id}` | Consulta o estoque (atual, reservada, disponível, baixo estoque) |
| `POST /api/estoque`, `PUT /api/estoque/{id}`, `DELETE /api/estoque/{id}` | Cadastro; item com reserva não é removido |
| `PATCH /api/estoque/{id}/reposicao` | Entrada de unidades |
| `GET/POST/PUT/DELETE /api/servicos-oficina`, `GET /api/servicos-oficina/ativos` | Catálogo de serviços da oficina |
| `GET /api/execucao/fila?status=` | OS em andamento, na ordem de atendimento |
| `GET /api/execucao/{ordemServicoId}` | OS com itens do diagnóstico e histórico |
| `POST /api/execucao` | Manual: entrada na fila de diagnóstico (papel do comando IniciarDiagnostico) |
| `PATCH /api/execucao/{id}/iniciar-diagnostico` | Mecânico assume o diagnóstico |
| `POST /api/execucao/{id}/diagnostico` | Registra peças, insumos e serviços, reserva o estoque e publica DiagnosticoConcluido |
| `PATCH /api/execucao/{id}/liberar-execucao` | Manual: pagamento confirmado (papel do comando IniciarExecucao), baixa o estoque |
| `PATCH /api/execucao/{id}/iniciar`, `PATCH /api/execucao/{id}/finalizar` | Reparo (publicam ExecucaoIniciada e ExecucaoFinalizada) |
| `PATCH /api/execucao/{id}/falha-diagnostico` | O diagnóstico não pode ser feito: devolve as reservas, cancela e publica DiagnosticoFalhou |
| `PATCH /api/execucao/{id}/cancelar` | Manual: devolve as reservas e cancela (papel do comando LiberarReservaEstoque) |

Erros seguem o envelope `{ "success": false, "errors": [...] }`: regra de negócio e estoque
insuficiente respondem 400, recurso inexistente 404 e conflito de gravação concorrente 409.

## Mensageria (Saga)

Com `Mensageria:Habilitada=true` (ligada no compose), o serviço consome os comandos que a Saga do
OS Service publica e responde com os eventos do [contrato](docs/contratos-eventos.md):

| Comando (fila) | O que a Execução faz | Resposta |
|---|---|---|
| `IniciarDiagnostico` (`execucao-iniciar-diagnostico`) | Põe a OS na fila de diagnóstico; se ela já tinha diagnóstico, reabre e devolve as reservas | `DiagnosticoConcluido` quando o mecânico registra o diagnóstico; `DiagnosticoFalhou` se ele registra a falha ou se a OS já passou do diagnóstico |
| `IniciarExecucao` (`execucao-iniciar-execucao`) | Baixa o estoque reservado e põe a OS na fila de reparo | `ExecucaoIniciada` e `ExecucaoFinalizada` conforme o mecânico avança; `ExecucaoFalhou` (com as reservas devolvidas) se não houve diagnóstico ou faltou saldo |
| `LiberarReservaEstoque` (`execucao-liberar-reserva-estoque`) | Devolve as reservas e cancela a OS na Execução | Nenhuma |

- JSON puro nos exchanges `MechanicLtda.Contratos:<Mensagem>`, igual ao OS Service; os eventos
  levam os cabeçalhos `correlation-id` (a Saga) e `x-correlation-id` (a requisição de origem).
- Cada mensagem é tratada uma vez: a tabela `execucao-mensagens` guarda o nome, a Saga, a OS e a
  `dataHora` de cada comando tratado, com a resposta publicada (TTL de 7 dias). Uma entrega
  repetida republica a mesma resposta; um reenvio legítimo, como o `IniciarDiagnostico` depois de
  um `OrcamentoFalhou`, tem outra `dataHora` e é tratado.
- Erro técnico tenta de novo (5 vezes, intervalo crescente) e depois vai para a fila `_error`; os
  eventos publicados por um consumidor só saem se ele terminar sem erro (outbox em memória).
- New Relic: `ExecucaoPassoConcluido` em cada mudança de status e `ExecucaoFalhou` em cada falha
  devolvida à Saga.

**Modo automático** (`Execucao:ModoAutomatico=true`, para a demonstração): a cada
`Execucao:SegundosPorPasso` segundos, toda OS parada avança um passo sem um mecânico: inicia e
conclui o diagnóstico (com o primeiro serviço ativo do catálogo e as peças do estoque citadas na
descrição do problema, a `Execucao:ValorPecaAutomatico` cada), inicia e finaliza o reparo. Uma
peça citada sem saldo vira `DiagnosticoFalhou`.

## Testes

```bash
dotnet test ExecucaoService.sln --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

Os testes de integração rodam contra um DynamoDB Local, com tabelas de prefixo único que são
apagadas no fim. Sem `DYNAMODB_SERVICE_URL`, eles aparecem como ignorados:

```bash
docker compose up -d dynamodb
DYNAMODB_SERVICE_URL=http://localhost:8000 dotnet test tests/ExecucaoService.IntegrationTests
```
