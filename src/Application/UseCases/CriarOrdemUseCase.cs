using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Application.Idempotency;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CaseGig.Application.UseCases;

public sealed class CriarOrdemUseCase
{
    private readonly ILogger<CriarOrdemUseCase> _logger;
    private readonly ITransactionManager _transactionManager;
    private readonly IClienteRepository _clienteRepository;
    private readonly IFundoRepository _fundoRepository;
    private readonly IPosicaoRepository _posicaoRepository;
    private readonly IOrdemRepository _ordemRepository;
    private readonly IdempotencyService _idempotencyService;

    public CriarOrdemUseCase(
        ILogger<CriarOrdemUseCase> logger,
        ITransactionManager transactionManager,
        IClienteRepository clienteRepository,
        IFundoRepository fundoRepository,
        IPosicaoRepository posicaoRepository,
        IOrdemRepository ordemRepository,
        IdempotencyService idempotencyService)
    {
        _logger = logger;
        _transactionManager = transactionManager;
        _clienteRepository = clienteRepository;
        _fundoRepository = fundoRepository;
        _posicaoRepository = posicaoRepository;
        _ordemRepository = ordemRepository;
        _idempotencyService = idempotencyService;
    }

    public async Task<CriarOrdemExecutionResult> ExecuteAsync(
        CriarOrdemRequestDto request,
        DateTime agora,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executando caso de uso: criar ordem. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} QuantidadeCotas={QuantidadeCotas}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas);

        var normalizedKey = _idempotencyService.NormalizeKey(idempotencyKey);
        var idempotencyOperation = "POST /ordens";
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

                var ordem = cliente.CriarOrdemImediata(fundo, posicao, request.TipoOperacao, request.QuantidadeCotas, agora);

                var idempotency = normalizedKey is null
                    ? null
                    : new IdempotencyMetadata(idempotencyOperation, normalizedKey, idempotencyRequestHash!);

                await _ordemRepository.AddAsync(ordem, idempotency, ct);

                if (ordem.Status == StatusOrdem.CRIADA)
                {
                    ordem.Processar(cliente, fundo, posicao, agora);
                }

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

        _logger.LogInformation("Ordem criada. Ordem={IdOrdem} Status={Status}", result!.IdOrdem, result!.Status);
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

    private static string ComputeRequestHash(CriarOrdemRequestDto request)
    {
        var payload = $"{request.IdCliente:N}|{request.IdFundo:N}|{request.TipoOperacao}|{request.QuantidadeCotas.ToString(CultureInfo.InvariantCulture)}";
        return RequestHash.ComputeSha256Hex(payload);
    }
}

public sealed record CriarOrdemExecutionResult(CriarOrdemResultDto Result, bool IsReplay);
