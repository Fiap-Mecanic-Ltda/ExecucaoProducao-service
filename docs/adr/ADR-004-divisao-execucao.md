# ADR-004 — O que sai do MechanicLtda para a Execução

- **Status:** aceito
- **Data:** 2026-11-09

## Contexto

O PDF da Fase 4 define o serviço de Execução e Produção como responsável por gerenciar a fila de
execução da OS, atualizar o status durante diagnóstico e reparos e comunicar a finalização ao OS
Service. O contrato da Saga já trata `estoqueId` e `servicoOficinaId` como ids da Execução.

Hoje tudo isso está no monolito MechanicLtda, junto com clientes, veículos, usuários, login e a
aplicação Web. A Ordem de Serviço já foi extraída para o OS Service.

## Decisão

1. A Execução é um **serviço novo**, com três repositórios próprios (`ExecucaoProducao-service`,
   `-InfraKubernete`, `-InfraSGBD`), no mesmo padrão do OS Service e do OrcamentoPagamento.
2. Passam para a Execução:
   - a **fila de execução** (nova): uma entrada por OS, do diagnóstico à finalização, com histórico;
   - o **diagnóstico**: o mecânico registra os itens e o serviço publica `DiagnosticoConcluido`;
   - o **estoque** de peças e insumos, agora com reserva no diagnóstico, baixa na execução e
     liberação na compensação;
   - o **catálogo de serviços** da oficina, com as mesmas rotas `/api/servicos-oficina`.
3. Ficam no MechanicLtda: clientes, veículos, usuários, login e a aplicação Web, que passa a chamar
   o OS Service e a Execução por HTTP.
4. Quando o cliente recusa o orçamento, a OS é cancelada e a Execução recebe
   `LiberarReservaEstoque` (decisão de 09/11/2026).

## Alternativas

| Alternativa | Por que não |
|---|---|
| MechanicLtda enxugado como Execução | Menos trabalho, mas mistura cadastro, login e Web com a fila de execução e mantém o SQL Server atual, deixando o NoSQL para o Billing |
| Estoque num quarto serviço | Separação maior do que o PDF pede; a reserva de estoque é parte do mesmo fluxo de execução |

## Consequências

- Estoque e catálogo saem do banco do MechanicLtda; os dados atuais são carregados por script na
  virada, mantendo os ids.
- Até a virada, o OS Service continua com o simulador da Execução; desligá-lo é só configuração.
- O Web passa a depender da Execução para as telas de estoque e serviços.
