# language: pt
Funcionalidade: Fila de execução
  Como mecânico da oficina
  Quero receber as OS da Saga, registrar o diagnóstico e executar o reparo
  Para que o OS Service acompanhe cada passo e o estoque reflita o que foi usado

  Contexto:
    Dado que a mensageria da Saga está ligada
    E que estou autenticado como funcionário
    E um item de estoque "Pastilha" com 5 unidades
    E o serviço "Troca de pastilhas" de 120,00 no catálogo

  Cenário: OS vai do diagnóstico à finalização
    Quando a Saga envia IniciarDiagnostico para a OS 101 com o problema "Freio chiando"
    Então a OS 101 está "AguardandoDiagnostico" na fila de execução
    Quando o mecânico registra o diagnóstico da OS 101 com 2 "Pastilha" a 89,90 e o serviço "Troca de pastilhas"
    Então a Execução publica DiagnosticoConcluido da OS 101 com o valor total 299,80
    E o item "Pastilha" tem 3 disponíveis e 2 reservadas
    Quando a Saga envia IniciarExecucao para a OS 101
    Então a OS 101 está "NaFila" na fila de execução
    E o item "Pastilha" tem 3 unidades e 0 reservadas
    Quando o mecânico inicia e finaliza o reparo da OS 101
    Então a Execução publica ExecucaoIniciada e ExecucaoFinalizada da OS 101
    E a OS 101 está "Finalizada" na Execução

  Cenário: Diagnóstico reaberto quando o orçamento falha
    Quando a Saga envia IniciarDiagnostico para a OS 102 com o problema "Ruído na roda"
    E o mecânico registra o diagnóstico da OS 102 com 2 "Pastilha" a 89,90 e o serviço "Troca de pastilhas"
    E a Saga reenvia IniciarDiagnostico para a OS 102
    Então a OS 102 está "EmDiagnostico" na Execução
    E o item "Pastilha" tem 5 disponíveis e 0 reservadas
