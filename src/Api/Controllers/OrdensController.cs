using CaseGig.Api.Contracts;
using CaseGig.Application.DTOs;
using CaseGig.Application.UseCases;
using CaseGig.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CaseGig.Api.Controllers;

[ApiController]
[Route("ordens")]
public sealed class OrdensController : ControllerBase
{
    private readonly ILogger<OrdensController> _logger;
    private readonly CriarOrdemUseCase _criarOrdemUseCase;
    private readonly CriarOrdemAgendadaUseCase _criarOrdemAgendadaUseCase;
    private readonly ConsultarOrdensUseCase _consultarOrdensUseCase;

    public OrdensController(
        ILogger<OrdensController> logger,
        CriarOrdemUseCase criarOrdemUseCase,
        CriarOrdemAgendadaUseCase criarOrdemAgendadaUseCase,
        ConsultarOrdensUseCase consultarOrdensUseCase)
    {
        _logger = logger;
        _criarOrdemUseCase = criarOrdemUseCase;
        _criarOrdemAgendadaUseCase = criarOrdemAgendadaUseCase;
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
            "Criando ordem. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} QuantidadeCotas={QuantidadeCotas}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas);

        var dto = new CriarOrdemRequestDto(
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas);

        var result = await _criarOrdemUseCase.ExecuteAsync(dto, DateTime.Now, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CriarOrdemResultDto>.Ok(result));
    }

    [HttpPost("agendamento")]
    [ProducesResponseType(typeof(ApiResponse<CriarOrdemResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<CriarOrdemResultDto>>> CriarOrdemAgendada([FromBody] CriarOrdemAgendamentoRequest request, CancellationToken cancellationToken)
    {
        var errors = ValidarCriarOrdemAgendadaRequest(request);
        if (errors.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Fail(errors.ToArray()));
        }

        _logger.LogInformation(
            "Criando ordem AGENDADA. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} Data={DataAgendamento}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.DataAgendamento.Date);

        var dto = new CriarOrdemAgendamentoRequestDto(
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas,
            request.DataAgendamento.Date);

        var result = await _criarOrdemAgendadaUseCase.ExecuteAsync(dto, DateTime.Now, cancellationToken);
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

        if (request.QuantidadeCotas <= 0)
        {
            errors.Add("QuantidadeCotas é obrigatória e deve ser maior que zero.");
        }

        return errors;
    }

    private static List<string> ValidarCriarOrdemAgendadaRequest(CriarOrdemAgendamentoRequest request)
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

        if (request.QuantidadeCotas <= 0)
        {
            errors.Add("QuantidadeCotas é obrigatória e deve ser maior que zero.");
        }

        if (request.DataAgendamento.Date <= DateTime.Today)
        {
            errors.Add("DataAgendamento deve ser futura (D+1 ou adiante).");
        }

        if (request.DataAgendamento.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            errors.Add("DataAgendamento deve ser um dia útil (segunda a sexta).");
        }

        return errors;
    }
}
