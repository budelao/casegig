using CaseGig.Api.Contracts;
using CaseGig.Application.DTOs;
using CaseGig.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace CaseGig.Api.Controllers;

[ApiController]
[Route("posicoes")]
public sealed class PosicoesController : ControllerBase
{
    private readonly ConsultarPosicoesUseCase _consultarPosicoesUseCase;
    private readonly ILogger<PosicoesController> _logger;

    public PosicoesController(ConsultarPosicoesUseCase consultarPosicoesUseCase, ILogger<PosicoesController> logger)
    {
        _consultarPosicoesUseCase = consultarPosicoesUseCase;
        _logger = logger;
    }

    [HttpGet("{idCliente:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosicaoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosicaoDto>>>> ConsultarPosicoes(Guid idCliente, CancellationToken cancellationToken)
    {
        _logger.LogInformation("API: Consultando posições. Cliente={IdCliente}", idCliente);
        var posicoes = await _consultarPosicoesUseCase.ExecuteAsync(idCliente, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PosicaoDto>>.Ok(posicoes));
    }
}
