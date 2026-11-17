namespace ExecucaoService.Bootstrap.Authorization
{
    /// <summary>
    /// Roles do JWT, iguais às do MechanicLtda e da Lambda de autenticação.
    /// </summary>
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Funcionario   = "Funcionario";
        public const string Cliente       = "Cliente";

        /// <summary>Roles com acesso administrativo completo.</summary>
        public const string Admin = $"{Administrador},{Funcionario}";

        /// <summary>Roles que podem consultar o andamento de uma OS na fila de execução.</summary>
        public const string AdminOuCliente = $"{Administrador},{Funcionario},{Cliente}";
    }
}
