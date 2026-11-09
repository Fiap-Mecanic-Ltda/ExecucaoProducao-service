using ExecucaoService.Domain.Entities;

namespace ExecucaoService.Application.DTOs
{
    public sealed record ServicoOficinaInputDto(string Nome, string? Descricao, decimal ValorBase, bool Ativo = true);

    public sealed record ServicoOficinaDto(
        int Id, string Nome, string? Descricao, decimal ValorBase, bool Ativo, DateTime DataCadastro, DateTime? DataAtualizacao)
    {
        public static ServicoOficinaDto De(ServicoOficina s) =>
            new(s.Id, s.Nome, s.Descricao, s.ValorBase, s.Ativo, s.DataCadastro, s.DataAtualizacao);
    }
}
