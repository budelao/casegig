using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Application.Idempotency;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CaseGig.Application.UseCases;

public sealed class CriarOrdemAgendadaUseCase
{
    private readonly ILogger<CriarOrdemAgendadaUseCase> _logger;
    private readonly ITransactionManager _transactionManager;
    private readonly IClienteRepository _clienteRepository;
    private readonly IFundoRepository _fundoRepository;
    private readonly IPosicaoRepository _posicaoRepository;
    private readonly IOrdemRepository _ordemRepository;
    private readonly IdempotencyService _idempotencyService;
    private readonly OrdemService _ordemService;

    public CriarOrdemAgendadaUseCase(
        ILogger<CriarOrdemAgendadaUseCase> logger,
        ITransactionManager transactionManager,
        IClienteRepository clienteRepository,
        IFundoRepository fundoRepository,
        IPosicaoRepository posicaoRepository,
        IOrdemRepository ordemRepository,
        IdempotencyService idempotencyService,
        OrdemService ordemService)
    {
        _logger = logger;
        _transactionManager = transactionManager;
        _clienteRepository = clienteRepository;
        _fundoRepository = fundoRepository;
        _posicaoRepository = posicaoRepository;
        _ordemRepository = ordemRepository;
        _idempotencyService = idempotencyService;
        _ordemService = ordemService;
    }

    public async Task<CriarOrdemExecutionResult> ExecuteAsync(
        CriarOrdemAgendamentoRequestDto request,
        DateTime agora,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executando caso de uso: criar ordem agendada. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} QuantidadeCotas={QuantidadeCotas} DataAgendamento={DataAgendamento}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas,
            request.DataAgendamento);

        var normalizedKey = _idempotencyService.NormalizeKey(idempotencyKey);
        var idempotencyOperation = "POST /ordens/agendamento";
        var idempotencyRequestHash = normalizedKey is null ? null : ComputeRequestHash(request);

        if (normalizedKey is not null)
        {
            var existing = await _idempotencyService.TryGetReplayAsync(
                request.IdCliente,
                idempotencyOperation,
                normalizedKey,
                idempotencyRequestHash!,
                cancellationToken);

            if (existing is not null)
            {
                return new CriarOrdemExecutionResult(Map(existing), true);
            }
        }

        CriarOrdemResultDto? result = null;

        try
        {
            await _transactionManager.ExecuteAsync(async ct =>
            {
                var cliente = await _clienteRepository.GetByIdAsync(request.IdCliente, ct);
                if (cliente is null)
                {
                    throw new NotFoundException("Cliente não encontrado.");
                }

                var fundo = await _fundoRepository.GetByIdAsync(request.IdFundo, ct);
                if (fundo is null)
                {
                    throw new NotFoundException("Fundo não encontrado.");
                }

                var posicao = await _posicaoRepository.GetByIdAsync(request.IdCliente, request.IdFundo, ct);

                var ordem = request.TipoOperacao switch
                {
                    TipoOperacao.APORTE => _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, request.QuantidadeCotas, request.DataAgendamento, agora),
                    TipoOperacao.RESGATE => _ordemService.CriarOrdemAgendadaResgate(cliente, fundo, posicao, request.QuantidadeCotas, request.DataAgendamento, agora),
                    _ => throw new BusinessRuleException("Tipo de operação inválido.")
                };

                if (normalizedKey is not null)
                {
                    ordem.IdempotencyKey = normalizedKey;
                    ordem.IdempotencyOperation = idempotencyOperation;
                    ordem.IdempotencyRequestHash = idempotencyRequestHash;
                }

                await _ordemRepository.AddAsync(ordem, ct);

                result = Map(ordem);
            }, cancellationToken);
        }
        catch
        {
            if (normalizedKey is not null)
            {
                var existing = await _idempotencyService.TryGetReplayAfterFailureAsync(
                    request.IdCliente,
                    idempotencyOperation,
                    normalizedKey,
                    idempotencyRequestHash!,
                    cancellationToken);

                if (existing is not null)
                {
                    return new CriarOrdemExecutionResult(Map(existing), true);
                }
            }

            throw;
        }

        _logger.LogInformation("Ordem agendada criada. Ordem={IdOrdem} Status={Status} DataAgendamento={DataAgendamento}", result!.IdOrdem, result!.Status, result!.DataAgendamento);
        return new CriarOrdemExecutionResult(result!, false);
    }

    private static CriarOrdemResultDto Map(Domain.Entities.Ordem ordem)
    {
        return new CriarOrdemResultDto(
            ordem.IdOrdem,
            ordem.IdCliente,
            ordem.IdFundo,
            ordem.TipoOperacao,
            ordem.Status,
            ordem.QuantidadeCotas,
            ordem.DataCriacao,
            ordem.DataAgendamento,
            ordem.DataProcessamento);
    }

    private static string ComputeRequestHash(CriarOrdemAgendamentoRequestDto request)
    {
        var payload = $"{request.IdCliente:N}|{request.IdFundo:N}|{request.TipoOperacao}|{request.QuantidadeCotas.ToString(CultureInfo.InvariantCulture)}|{request.DataAgendamento:yyyy-MM-dd}";
        return RequestHash.ComputeSha256Hex(payload);
    }
}
