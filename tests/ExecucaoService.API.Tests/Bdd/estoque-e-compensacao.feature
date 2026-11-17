# language: pt
Funcionalidade: Estoque e compensação
  Como responsável pela oficina
  Quero que falta de peça e desistências devolvam o estoque e avisem a Saga
  Para que nenhuma peça fique presa a uma OS que não vai ser executada

  Contexto:
    Dado que a mensageria da Saga está ligada
    E que estou autenticado como funcionário
    E um item de estoque "Amortecedor" com 1 unidades
    E o serviço "Troca de amortecedor" de 250,00 no catálogo

  Cenário: Peça sem saldo no diagnóstico vira DiagnosticoFalhou
    Quando a Saga envia IniciarDiagnostico para a OS 201 com o problema "Amortecedor batendo"
    E o mecânico tenta registrar o diagnóstico da OS 201 com 2 "Amortecedor" a 300,00 e o serviço "Troca de amortecedor"
    Então a API recusa com "Estoque insuficiente"
    Quando o mecânico registra a falha do diagnóstico da OS 201 com o motivo "Amortecedor em falta"
    Então a Execução publica DiagnosticoFalhou da OS 201 com o motivo "Amortecedor em falta"
    E a OS 201 está "Cancelada" na Execução
    E o item "Amortecedor" tem 1 disponíveis e 0 reservadas

  Cenário: Orçamento recusado devolve a reserva
    Quando a Saga envia IniciarDiagnostico para a OS 202 com o problema "Suspensão"
    E o mecânico registra o diagnóstico da OS 202 com 1 "Amortecedor" a 300,00 e o serviço "Troca de amortecedor"
    E a Saga envia LiberarReservaEstoque para a OS 202 com o motivo "Orçamento recusado"
    Então a OS 202 está "Cancelada" na Execução
    E o item "Amortecedor" tem 1 disponíveis e 0 reservadas
    E a Execução não publica falha para a OS 202

  Cenário: Pagamento de uma OS sem diagnóstico vira ExecucaoFalhou
    Quando a Saga envia IniciarDiagnostico para a OS 203 com o problema "Barulho"
    E a Saga envia IniciarExecucao para a OS 203
    Então a Execução publica ExecucaoFalhou da OS 203
    E a OS 203 está "Cancelada" na Execução
