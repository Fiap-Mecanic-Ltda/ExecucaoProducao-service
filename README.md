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
