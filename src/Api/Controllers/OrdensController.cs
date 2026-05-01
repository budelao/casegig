using CaseGig.Api.Contracts;
using CaseGig.Application.DTOs;
using CaseGig.Application.UseCases;
using CaseGig.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CaseGig.Api.Controllers;

[ApiController]
[Route("api/ordens")]
public sealed class OrdensController : ControllerBase
{
    private readonly ILogger<OrdensController> _logger;
    private readonly CriarOrdemUseCase _criarOrdemUseCase;
    private readonly ConsultarOrdensUseCase _consultarOrdensUseCase;

    public OrdensController(
        ILogger<OrdensController> logger,
        CriarOrdemUseCase criarOrdemUseCase,
        ConsultarOrdensUseCase consultarOrdensUseCase)
    {
        _logger = logger;
        _criarOrdemUseCase = criarOrdemUseCase;
        _consultarOrdensUseCase = consultarOrdensUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CriarOrdemResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<CriarOrdemResultDto>>> CriarOrdem([FromBody] CriarOrdemRequest request, CancellationToken cancellationToken)
    {
        var errors = ValidarCriarOrdemRequest(request);
        if (errors.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Fail(errors.ToArray()));
        }

        _logger.LogInformation(
            "Criando ordem. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao);

        var dto = new CriarOrdemRequestDto(
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.ValorAporte,
            request.QuantidadeCotas);

        var result = await _criarOrdemUseCase.ExecuteAsync(dto, DateTime.Now, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CriarOrdemResultDto>.Ok(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OrdemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrdemDto>>>> ConsultarOrdens([FromQuery] Guid idCliente, CancellationToken cancellationToken)
    {
        var ordens = await _consultarOrdensUseCase.ExecuteAsync(idCliente, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrdemDto>>.Ok(ordens));
    }

    private static List<string> ValidarCriarOrdemRequest(CriarOrdemRequest request)
    {
        var errors = new List<string>();

        if (request.IdCliente == Guid.Empty)
        {
            errors.Add("IdCliente é obrigatório.");
        }

        if (request.IdFundo == Guid.Empty)
        {
            errors.Add("IdFundo é obrigatório.");
        }

        if (request.TipoOperacao == TipoOperacao.APORTE)
        {
            if (request.ValorAporte is null || request.ValorAporte <= 0)
            {
                errors.Add("ValorAporte é obrigatório e deve ser maior que zero para APORTE.");
            }
        }
        else if (request.TipoOperacao == TipoOperacao.RESGATE)
        {
            if (request.QuantidadeCotas is null || request.QuantidadeCotas <= 0)
            {
                errors.Add("QuantidadeCotas é obrigatória e deve ser maior que zero para RESGATE.");
            }
        }

        return errors;
    }
}
