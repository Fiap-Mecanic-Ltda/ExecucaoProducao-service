// Os cenários BDD de features diferentes compartilham a mesma API em memória (e os mesmos
// repositórios falsos, que não são thread-safe): as classes de teste rodam uma de cada vez.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
