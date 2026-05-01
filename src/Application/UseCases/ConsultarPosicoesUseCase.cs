using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace CaseGig.Application.UseCases;

public sealed class ConsultarPosicoesUseCase
{
    private readonly ILogger<ConsultarPosicoesUseCase> _logger;
    private readonly IPosicaoRepository _posicaoRepository;

    public ConsultarPosicoesUseCase(ILogger<ConsultarPosicoesUseCase> logger, IPosicaoRepository posicaoRepository)
    {
        _logger = logger;
        _posicaoRepository = posicaoRepository;
    }

    public async Task<IReadOnlyList<PosicaoDto>> ExecuteAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executando caso de uso: consultar posições. Cliente={IdCliente}", idCliente);
        var posicoes = await _posicaoRepository.ListByClienteIdAsync(idCliente, cancellationToken);
        var result = posicoes
            .Select(p => new PosicaoDto(p.IdCliente, p.IdFundo, p.QuantidadeCotas))
            .ToList();
        _logger.LogInformation("Consulta de posições concluída. Cliente={IdCliente} Quantidade={Quantidade}", idCliente, result.Count);
        return result;
    }
}
