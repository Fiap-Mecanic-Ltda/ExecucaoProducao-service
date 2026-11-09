# Contratos de mensagens da Saga da Ordem de Serviço

Este documento é o contrato entre o **OS Service** (orquestrador da Saga) e os serviços
**Orçamento e Pagamento** (Billing) e **Execução e Produção**. Uma cópia idêntica deve existir
nos três repositórios; qualquer mudança aqui passa por PR nos três.

- Decisão de mensageria: [ADR-001](adr/ADR-001-mensageria-rabbitmq.md)
- Decisão da Saga: [ADR-002](adr/ADR-002-saga-orquestrada.md)

## Regras gerais

| Item | Regra |
|---|---|
| Broker | RabbitMQ (namespace `mensageria` do cluster k3s; `localhost:5672` no docker-compose) |
| Publicação | Toda mensagem é **publicada** num exchange `fanout` com o nome da mensagem, ex.: `MechanicLtda.Contratos:GerarOrcamento`. Quem consome cria a própria fila e faz o bind. Ninguém precisa saber o nome da fila do outro |
| Fila de cada consumidor | `<servico>-<mensagem-em-kebab>`, ex.: `billing-gerar-orcamento`, `os-orcamento-aprovado`. Uma fila por mensagem: assim o consumidor sabe o tipo pela fila e o JSON não precisa de cabeçalho de tipo |
| Corpo | JSON puro (sem envelope), UTF-8, propriedades em `camelCase`, datas em ISO 8601 UTC, valores monetários em `decimal` com 2 casas |
| Cabeçalhos | `correlation-id` (o mesmo `correlationId` do corpo) e `x-correlation-id` (o `X-Correlation-Id` HTTP que originou o fluxo, para o rastreio no New Relic) |
| Correlação | `correlationId` (GUID) identifica **a Saga de uma OS** e é gerado pelo OS Service na abertura. Toda resposta devolve o mesmo valor |
| Idempotência | A mesma mensagem pode chegar mais de uma vez (at-least-once). Consumidores tratam duplicatas pelo par `correlationId` + nome da mensagem. Preencher a propriedade AMQP `message_id` (GUID) é recomendado; o MassTransit faz isso sozinho |
| Falhas | Erro de negócio vira uma mensagem de falha (`...Falhou`). Erro técnico é reprocessado pelo consumidor (retry) e, esgotado, vai para a fila `_error` |
| Compatibilidade | Campo novo é opcional; remover ou renomear campo exige nova versão da mensagem (`...V2`) |

Campos presentes em **todas** as mensagens:

| Campo | Tipo | Descrição |
|---|---|---|
| `correlationId` | GUID | Id da Saga (uma por OS) |
| `ordemServicoId` | int | Id da OS no OS Service |
| `dataHora` | datetime (UTC) | Quando a mensagem foi gerada |

Nas tabelas abaixo esses três campos ficam implícitos.

## Tipos compartilhados

**`ItemOrdemServico`**

| Campo | Tipo | Descrição |
|---|---|---|
| `descricao` | string (até 1000) | Descrição do item |
| `tipo` | string | `Peca`, `Insumo` ou `Servico` |
| `quantidade` | int | Maior que zero |
| `valorUnitario` | decimal | Valor unitário no momento do diagnóstico |
| `estoqueId` | int? | Id no estoque da Execução, quando for peça ou insumo |
| `servicoOficinaId` | int? | Id no catálogo de serviços da Execução, quando for serviço |

## Comandos do OS Service

| Mensagem | Para | Campos | Respostas esperadas |
|---|---|---|---|
| `IniciarDiagnostico` | Execução | `clienteId`, `veiculoId`, `descricaoProblema` | `DiagnosticoConcluido` ou `DiagnosticoFalhou` |
| `GerarOrcamento` | Billing | `clienteId`, `nomeCliente`, `emailCliente`, `itens: ItemOrdemServico[]` | `OrcamentoGerado` ou `OrcamentoFalhou`; depois `OrcamentoAprovado` ou `OrcamentoRecusado` |
| `SolicitarPagamento` | Billing | `orcamentoId`, `valorTotal` | `PagamentoConfirmado` ou `PagamentoFalhou` |
| `IniciarExecucao` | Execução | `orcamentoId`, `itens: ItemOrdemServico[]` | `ExecucaoIniciada`, depois `ExecucaoFinalizada`; ou `ExecucaoFalhou` |
| `CancelarOrcamento` | Billing | `orcamentoId`, `motivo` | `OrcamentoCancelado` |
| `EstornarPagamento` | Billing | `pagamentoId`, `motivo` | `PagamentoEstornado` |
| `LiberarReservaEstoque` | Execução | `motivo` | nenhuma (melhor esforço, idempotente) |

`nomeCliente` e `emailCliente` vão no `GerarOrcamento` porque o Billing envia o e-mail de
aprovação e não deve consultar o banco de clientes. O OS Service obtém esses dados por REST na
abertura da OS.

## Eventos da Execução

| Mensagem | Campos | Efeito no OS Service |
|---|---|---|
| `DiagnosticoConcluido` | `itens: ItemOrdemServico[]`, `observacao?` | Grava os itens e vai para `AguardandoOrcamento`; envia `GerarOrcamento` |
| `DiagnosticoFalhou` | `motivo` | Volta para `Recebida` e registra a falha; a Saga termina e recomeça num novo `iniciar-diagnostico` |
| `ExecucaoIniciada` | `dataInicio` | Registra `dataInicioExecucao` (status continua `EmExecucao`) |
| `ExecucaoFinalizada` | `dataInicio`, `dataFim` | Vai para `Finalizada` |
| `ExecucaoFalhou` | `motivo` | Compensação: `EstornarPagamento` + `CancelarOrcamento`; vai para `Compensando` |

## Eventos do Billing

| Mensagem | Campos | Efeito no OS Service |
|---|---|---|
| `OrcamentoGerado` | `orcamentoId`, `valorTotal`, `validade` | Vai para `AguardandoAprovacao` |
| `OrcamentoFalhou` | `motivo` | Volta para `EmDiagnostico` e reenvia `IniciarDiagnostico` para a Execução refazer o diagnóstico |
| `OrcamentoAprovado` | `orcamentoId` | Vai para `AguardandoPagamento`; envia `SolicitarPagamento` |
| `OrcamentoRecusado` | `orcamentoId`, `motivo?` | Compensação: `LiberarReservaEstoque`; vai para `Cancelada` |
| `PagamentoConfirmado` | `pagamentoId`, `valor`, `meioPagamento` | Vai para `EmExecucao`; envia `IniciarExecucao` |
| `PagamentoFalhou` | `motivo` | Compensação: `CancelarOrcamento` + `LiberarReservaEstoque`; vai para `Compensando` |
| `OrcamentoCancelado` | `orcamentoId` | Parte da compensação |
| `PagamentoEstornado` | `pagamentoId` | Parte da compensação |

A compensação termina (status `Cancelada`) quando todas as confirmações pedidas chegaram
(`OrcamentoCancelado` e, se houve pagamento, `PagamentoEstornado`).

## Início, prazos e mensagens fora de ordem

- A Saga começa quando a OS vai para diagnóstico (`PATCH /api/ordemservico/{id}/iniciar-diagnostico`,
  ou o `PUT` com valor estimado). É nesse momento que o OS Service lê nome e e-mail do cliente.
- Prazos: a Execução tem `Saga:PrazoDiagnosticoHoras` (padrão 48 h) para responder ao
  `IniciarDiagnostico`. Vencido, o OS Service envia `LiberarReservaEstoque` e a OS volta para
  `Recebida`. A aprovação do orçamento vence em `validade` (do `OrcamentoGerado`) ou, sem ela, em
  `Saga:PrazoAprovacaoHoras` (padrão 72 h), com a compensação `CancelarOrcamento` +
  `LiberarReservaEstoque`.
- Cancelar a OS pela API durante o diagnóstico envia `LiberarReservaEstoque`. Depois do
  diagnóstico, o cancelamento manual fica bloqueado com a Saga ligada.
- Um evento repetido ou que chega num passo em que não é esperado é ignorado (fica no log). Um
  evento de uma Saga que já terminou é descartado.

## Evento publicado pelo OS Service

| Mensagem | Campos | Uso |
|---|---|---|
| `OrdemServicoStatusAlterado` | `clienteId`, `statusAnterior`, `statusAtual`, `motivo?` | Notificações e painéis; qualquer serviço pode assinar |

## Status da OS

| Valor | Nome | Observação |
|---|---|---|
| 1 | `Recebida` | Já existia |
| 2 | `EmDiagnostico` | Já existia |
| 3 | `AguardandoAprovacao` | Já existia |
| 4 | `EmExecucao` | Já existia |
| 5 | `Finalizada` | Já existia |
| 6 | `Entregue` | Já existia |
| 7 | `AguardandoOrcamento` | Novo: diagnóstico concluído, orçamento sendo gerado |
| 8 | `AguardandoPagamento` | Novo: orçamento aprovado, pagamento pendente |
| 9 | `Compensando` | Novo: falha em andamento, aguardando confirmações de compensação |
| 10 | `Cancelada` | Novo: estado final de uma Saga compensada ou recusada |

Os valores 1 a 6 são mantidos para não quebrar clientes da API da Fase 3.

## Exemplo

Exchange `MechanicLtda.Contratos:GerarOrcamento`, cabeçalho `correlation-id: 3f0c…`:

```json
{
  "correlationId": "3f0c2f6e-8a4b-4c1e-9a52-0b6f1d2e7c11",
  "ordemServicoId": 42,
  "dataHora": "2026-11-09T14:03:12Z",
  "clienteId": 7,
  "nomeCliente": "Maria Souza",
  "emailCliente": "maria@exemplo.com",
  "itens": [
    { "descricao": "Pastilha de freio dianteira", "tipo": "Peca", "quantidade": 2, "valorUnitario": 89.90, "estoqueId": 15, "servicoOficinaId": null },
    { "descricao": "Troca de pastilhas", "tipo": "Servico", "quantidade": 1, "valorUnitario": 120.00, "estoqueId": null, "servicoOficinaId": 3 }
  ]
}
```
