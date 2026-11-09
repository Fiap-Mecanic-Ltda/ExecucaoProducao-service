using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Domain.Entities
{
    /// <summary>Serviço do catálogo da oficina (ex.: troca de pastilhas), com o valor base cobrado.</summary>
    public class ServicoOficina
    {
        public const int TamanhoMaximoNome = 200;
        public const int TamanhoMaximoDescricao = 1000;

        public int Id { get; private set; }
        public string Nome { get; private set; } = string.Empty;
        public string? Descricao { get; private set; }
        public decimal ValorBase { get; private set; }
        public bool Ativo { get; private set; } = true;
        public DateTime DataCadastro { get; private set; }
        public DateTime? DataAtualizacao { get; private set; }

        private ServicoOficina() { }

        public static ServicoOficina Criar(int id, string nome, string? descricao, decimal valorBase, DateTime agora)
        {
            var servico = new ServicoOficina { Id = id, DataCadastro = agora, Ativo = true };
            servico.Definir(nome, descricao, valorBase);
            return servico;
        }

        public static ServicoOficina Restaurar(int id, string nome, string? descricao, decimal valorBase, bool ativo,
                                               DateTime dataCadastro, DateTime? dataAtualizacao) =>
            new()
            {
                Id              = id,
                Nome            = nome,
                Descricao       = descricao,
                ValorBase       = valorBase,
                Ativo           = ativo,
                DataCadastro    = dataCadastro,
                DataAtualizacao = dataAtualizacao
            };

        public void Atualizar(string nome, string? descricao, decimal valorBase, bool ativo, DateTime agora)
        {
            Definir(nome, descricao, valorBase);
            Ativo           = ativo;
            DataAtualizacao = agora;
        }

        private void Definir(string nome, string? descricao, decimal valorBase)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new RegraDeNegocioException("O nome do serviço é obrigatório.");
            if (nome.Trim().Length > TamanhoMaximoNome)
                throw new RegraDeNegocioException($"O nome do serviço tem no máximo {TamanhoMaximoNome} caracteres.");
            if (descricao is not null && descricao.Length > TamanhoMaximoDescricao)
                throw new RegraDeNegocioException($"A descrição do serviço tem no máximo {TamanhoMaximoDescricao} caracteres.");
            if (valorBase < 0)
                throw new RegraDeNegocioException("O valor base do serviço não pode ser negativo.");

            Nome      = nome.Trim();
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
            ValorBase = decimal.Round(valorBase, 2);
        }
    }
}
