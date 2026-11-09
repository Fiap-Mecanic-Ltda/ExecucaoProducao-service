# ADR-003 — DynamoDB como banco NoSQL da Execução

- **Status:** aceito
- **Data:** 2026-11-09

## Contexto

A Fase 4 exige banco próprio por microsserviço e pelo menos um banco relacional e um não
relacional. O OS Service usa SQL Server. Os repositórios do Billing ainda não têm código, então o
requisito de NoSQL não pode depender dele.

Os acessos da Execução são sempre por chave ou por status: a entrada de uma OS na fila e o seu
histórico, a fila filtrada por status, um item de estoque, o catálogo. Não há joins. A única
operação que exige cuidado é a reserva e a baixa de estoque, que precisam ser atômicas.

## Decisão

1. **Amazon DynamoDB** em modo sob demanda, criado por Terraform no `ExecucaoProducao-InfraSGBD`.
2. Tabelas:

   | Tabela | Chave (PK / SK) | Conteúdo | Índice |
   |---|---|---|---|
   | `execucao-ordens` | `OS#<ordemServicoId>` / `EXECUCAO` | Estado da OS na Execução: status, `correlationId`, itens, reservas, datas | GSI `status` + `dataEntrada` (fila) |
   | `execucao-ordens` | `OS#<ordemServicoId>` / `HIST#<dataHora>` | Uma linha por mudança de status | — |
   | `execucao-estoque` | `ESTOQUE#<id>` | Nome, tipo, quantidade atual, reservada e mínima | GSI `tipo` |
   | `execucao-servicos` | `SERVICO#<id>` | Catálogo de serviços da oficina | — |
   | `execucao-mensagens` | `<mensagem>#<correlationId>#<ordemServicoId>#<dataHora>` | Mensagens processadas e a resposta publicada, TTL de 7 dias (`expiraEm`) | — |

3. Reserva e baixa de estoque usam **escrita condicional**
   (`quantidadeAtual - quantidadeReservada >= :n`), sem lock na aplicação.
4. Os ids continuam `int`, como o contrato pede (`estoqueId`, `servicoOficinaId`), gerados por um
   contador atômico na própria tabela. Os itens migrados do MechanicLtda mantêm os ids atuais.
5. Localmente e nos testes de repositório, a imagem `amazon/dynamodb-local` no docker-compose.
6. Na AWS, os pods acessam as tabelas pela role da instância do k3s, com uma política IAM restrita
   às tabelas `execucao-*`. Não existe senha de banco.

## Alternativas

| Alternativa | Por que não |
|---|---|
| MongoDB no k3s | Mais um StatefulSet com volume para operar no cluster pequeno; backup e réplica ficariam por nossa conta |
| MongoDB Atlas | Conta e rede externas à AWS do projeto, fora do padrão Terraform dos repositórios InfraSGBD |
| SQL Server (RDS) | Atende o domínio, mas deixa o requisito de NoSQL dependendo do Billing e custa uma segunda instância |

## Consequências

- **Sem outbox transacional:** o MassTransit não tem outbox para DynamoDB. O serviço usa o outbox
  em memória do MassTransit (publica só depois que o consumidor termina sem erro) e consumidores
  idempotentes pela tabela `execucao-mensagens`. Uma mensagem repetida republica a mesma resposta
  em vez de processar de novo, o que cobre a falha entre gravar e publicar. A `dataHora` entra na
  chave porque o OS Service reenvia `IniciarDiagnostico` com o mesmo `correlationId` depois de um
  `OrcamentoFalhou`; uma entrega repetida tem o mesmo corpo, um reenvio tem outra `dataHora`.
- Consultas novas exigem pensar no índice antes; relatórios ad hoc não são o uso deste banco.
- Custo praticamente zero no volume da demonstração (nível gratuito do modo sob demanda).
