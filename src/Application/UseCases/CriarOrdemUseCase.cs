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

public sealed class CriarOrdemUseCase
{
    private readonly ILogger<CriarOrdemUseCase> _logger;
    private readonly ITransactionManager _transactionManager;
    private readonly IClienteRepository _clienteRepository;
    private readonly IFundoRepository _fundoRepository;
    private readonly IPosicaoRepository _posicaoRepository;
    private readonly IOrdemRepository _ordemRepository;
    private readonly OrdemService _ordemService;
    private readonly OrdemProcessamentoService _processamentoService;

    public CriarOrdemUseCase(
        ILogger<CriarOrdemUseCase> logger,
        ITransactionManager transactionManager,
        IClienteRepository clienteRepository,
        IFundoRepository fundoRepository,
        IPosicaoRepository posicaoRepository,
        IOrdemRepository ordemRepository,
        OrdemService ordemService,
        OrdemProcessamentoService processamentoService)
    {
        _logger = logger;
        _transactionManager = transactionManager;
        _clienteRepository = clienteRepository;
        _fundoRepository = fundoRepository;
        _posicaoRepository = posicaoRepository;
        _ordemRepository = ordemRepository;
        _ordemService = ordemService;
        _processamentoService = processamentoService;
    }

    public async Task<CriarOrdemExecutionResult> ExecuteAsync(
        CriarOrdemRequestDto request,
        DateTime agora,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "API: Executando caso de uso: criar ordem. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} QuantidadeCotas={QuantidadeCotas}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas);

        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var idempotencyOperation = "POST /ordens";
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
                    TipoOperacao.APORTE => CriarOrdemAporte(cliente, fundo, request, agora),
                    TipoOperacao.RESGATE => CriarOrdemResgate(cliente, fundo, posicao, request, agora),
                    _ => throw new BusinessRuleException("Tipo de operação inválido.")
                };

                if (normalizedKey is not null)
                {
                    ordem.IdempotencyKey = normalizedKey;
                    ordem.IdempotencyOperation = idempotencyOperation;
                    ordem.IdempotencyRequestHash = idempotencyRequestHash;
                }

                await _ordemRepository.AddAsync(ordem, ct);

                if (ordem.Status == StatusOrdem.CRIADA)
                {
                    _processamentoService.PrepararParaProcessamento(ordem, agora);

                    if (ordem.TipoOperacao == TipoOperacao.APORTE)
                    {
                        _processamentoService.ProcessarOrdemAporte(ordem, cliente, fundo, posicao);
                    }
                    else
                    {
                        if (posicao is null)
                        {
                            throw new BusinessRuleException("Posição do cliente no fundo não encontrada.");
                        }

                        _processamentoService.ProcessarOrdemResgate(ordem, cliente, fundo, posicao);
                    }

                    _processamentoService.Concluir(ordem, agora);
                }

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

        _logger.LogInformation("API: Ordem criada. Ordem={IdOrdem} Status={Status}", result!.IdOrdem, result!.Status);
        return new CriarOrdemExecutionResult(result!, false);
    }

    private Domain.Entities.Ordem CriarOrdemAporte(Domain.Entities.Cliente cliente, Domain.Entities.Fundo fundo, CriarOrdemRequestDto request, DateTime agora)
    {
        return _ordemService.CriarOrdemAportePorCotas(cliente, fundo, request.QuantidadeCotas, agora);
    }

    private Domain.Entities.Ordem CriarOrdemResgate(
        Domain.Entities.Cliente cliente,
        Domain.Entities.Fundo fundo,
        Domain.Entities.Posicao? posicao,
        CriarOrdemRequestDto request,
        DateTime agora)
    {
        return _ordemService.CriarOrdemResgate(cliente, fundo, posicao, request.QuantidadeCotas, agora);
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

    private static string ComputeRequestHash(CriarOrdemRequestDto request)
    {
        var payload = $"{request.IdCliente:N}|{request.IdFundo:N}|{request.TipoOperacao}|{request.QuantidadeCotas.ToString(CultureInfo.InvariantCulture)}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

public sealed record CriarOrdemExecutionResult(CriarOrdemResultDto Result, bool IsReplay);
