# ADR-001 — Mensageria com RabbitMQ e MassTransit

- **Status:** proposto
- **Data:** 2026-11-09
- **Substitui:** ADR-005 do MechanicLtda ("comunicação síncrona, sem mensageria")

## Contexto

A Fase 4 divide a aplicação em microsserviços (Ordem de Serviço, Orçamento e Pagamento,
Execução e Produção), cada um com banco próprio, e exige mensageria assíncrona para integração e
para a Saga. Nenhum serviço pode acessar o banco de outro.

## Decisão

1. **RabbitMQ** é o broker, rodando no próprio cluster k3s (namespace `mensageria`, StatefulSet com
   volume persistente), com credenciais no SSM Parameter Store como os demais segredos.
2. No OS Service, a integração usa **MassTransit 8.x** (licença Apache 2.0): state machine de Saga
   persistida no SQL Server, **outbox transacional** (status da OS e mensagem gravados na mesma
   transação), retry com backoff e fila de erro.
3. O formato na fila é **JSON puro** e cada mensagem tem um exchange com nome fixo
   ([contratos-eventos.md](../contratos-eventos.md)), para que os outros serviços não precisem usar
   MassTransit nem .NET.
4. Chamadas **REST síncronas** continuam onde a resposta é imediata: consultas e a validação de
   cliente e veículo na abertura da OS.

## Alternativas

| Alternativa | Por que não |
|---|---|
| Amazon SNS + SQS | Gerenciado e sem operação, mas o ambiente local exigiria LocalStack e o fluxo fica menos visível na demonstração. Continua viável: o MassTransit suporta SQS com a mesma Saga |
| Kafka | Operação pesada para o tamanho do cluster (EC2 pequena); o volume de mensagens não justifica um log distribuído |
| Só REST entre os serviços | Acopla a disponibilidade dos três serviços e não dá retry nem compensação assíncrona |
| MassTransit 9 | Licença comercial |

## Consequências

- Um componente a mais no cluster (RabbitMQ), com memória reservada e volume persistente.
- O mesmo `docker-compose` sobe o broker localmente, e o painel de gestão mostra filas e mensagens
  no vídeo de demonstração.
- Consumidores precisam ser idempotentes (entrega at-least-once).
