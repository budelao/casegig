using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CaseGig.Application.UseCases;

public sealed class CriarOrdemAgendadaUseCase
{
    private readonly ILogger<CriarOrdemAgendadaUseCase> _logger;
    private readonly ITransactionManager _transactionManager;
    private readonly IClienteRepository _clienteRepository;
    private readonly IFundoRepository _fundoRepository;
    private readonly IPosicaoRepository _posicaoRepository;
    private readonly IOrdemRepository _ordemRepository;
    private readonly OrdemService _ordemService;

    public CriarOrdemAgendadaUseCase(
        ILogger<CriarOrdemAgendadaUseCase> logger,
        ITransactionManager transactionManager,
        IClienteRepository clienteRepository,
        IFundoRepository fundoRepository,
        IPosicaoRepository posicaoRepository,
        IOrdemRepository ordemRepository,
        OrdemService ordemService)
    {
        _logger = logger;
        _transactionManager = transactionManager;
        _clienteRepository = clienteRepository;
        _fundoRepository = fundoRepository;
        _posicaoRepository = posicaoRepository;
        _ordemRepository = ordemRepository;
        _ordemService = ordemService;
    }

    public async Task<CriarOrdemExecutionResult> ExecuteAsync(
        CriarOrdemAgendamentoRequestDto request,
        DateTime agora,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "API: Executando caso de uso: criar ordem agendada. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} QuantidadeCotas={QuantidadeCotas} DataAgendamento={DataAgendamento}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas,
            request.DataAgendamento);

        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var idempotencyOperation = "POST /ordens/agendamento";
        var idempotencyRequestHash = normalizedKey is null ? null : ComputeRequestHash(request);

        if (normalizedKey is not null)
        {
            var existing = await _ordemRepository.GetByIdempotencyAsync(request.IdCliente, idempotencyOperation, normalizedKey, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.IdempotencyRequestHash, idempotencyRequestHash, StringComparison.Ordinal))
                {
                    throw new ConcurrencyException("Idempotency-Key já utilizada com payload diferente.");
                }

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
                var existing = await _ordemRepository.GetByIdempotencyAsync(request.IdCliente, idempotencyOperation, normalizedKey, cancellationToken);
                if (existing is not null && string.Equals(existing.IdempotencyRequestHash, idempotencyRequestHash, StringComparison.Ordinal))
                {
                    return new CriarOrdemExecutionResult(Map(existing), true);
                }
            }

            throw;
        }

        _logger.LogInformation("API: Ordem agendada criada. Ordem={IdOrdem} Status={Status} DataAgendamento={DataAgendamento}", result!.IdOrdem, result!.Status, result!.DataAgendamento);
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

    private static string? NormalizeIdempotencyKey(string? key)
    {
        var trimmed = key?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string ComputeRequestHash(CriarOrdemAgendamentoRequestDto request)
    {
        var payload = $"{request.IdCliente:N}|{request.IdFundo:N}|{request.TipoOperacao}|{request.QuantidadeCotas.ToString(CultureInfo.InvariantCulture)}|{request.DataAgendamento:yyyy-MM-dd}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
