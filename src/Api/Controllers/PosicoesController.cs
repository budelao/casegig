using CaseGig.Api.Contracts;
using CaseGig.Application.DTOs;
using CaseGig.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace CaseGig.Api.Controllers;

[ApiController]
[Route("api/posicoes")]
public sealed class PosicoesController : ControllerBase
{
    private readonly ConsultarPosicoesUseCase _consultarPosicoesUseCase;

    public PosicoesController(ConsultarPosicoesUseCase consultarPosicoesUseCase)
    {
        _consultarPosicoesUseCase = consultarPosicoesUseCase;
    }

    [HttpGet("{idCliente:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosicaoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosicaoDto>>>> ConsultarPosicoes(Guid idCliente, CancellationToken cancellationToken)
    {
        var posicoes = await _consultarPosicoesUseCase.ExecuteAsync(idCliente, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PosicaoDto>>.Ok(posicoes));
    }
}
