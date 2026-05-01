using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;

namespace CaseGig.Application.UseCases;

public sealed class ConsultarPosicoesUseCase
{
    private readonly IPosicaoRepository _posicaoRepository;

    public ConsultarPosicoesUseCase(IPosicaoRepository posicaoRepository)
    {
        _posicaoRepository = posicaoRepository;
    }

    public async Task<IReadOnlyList<PosicaoDto>> ExecuteAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        var posicoes = await _posicaoRepository.ListByClienteIdAsync(idCliente, cancellationToken);
        return posicoes
            .Select(p => new PosicaoDto(p.IdCliente, p.IdFundo, p.QuantidadeCotas))
            .ToList();
    }
}
