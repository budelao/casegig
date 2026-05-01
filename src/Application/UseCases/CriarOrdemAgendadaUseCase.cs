using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;
using Microsoft.Extensions.Logging;

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

    public async Task<CriarOrdemResultDto> ExecuteAsync(CriarOrdemAgendamentoRequestDto request, DateTime agora, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executando caso de uso: criar ordem agendada. Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao} QuantidadeCotas={QuantidadeCotas} DataAgendamento={DataAgendamento}",
            request.IdCliente,
            request.IdFundo,
            request.TipoOperacao,
            request.QuantidadeCotas,
            request.DataAgendamento);

        CriarOrdemResultDto? result = null;

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

            await _ordemRepository.AddAsync(ordem, ct);

            result = new CriarOrdemResultDto(
                ordem.IdOrdem,
                ordem.IdCliente,
                ordem.IdFundo,
                ordem.TipoOperacao,
                ordem.Status,
                ordem.QuantidadeCotas,
                ordem.DataCriacao,
                ordem.DataAgendamento,
                ordem.DataProcessamento);
        }, cancellationToken);

        _logger.LogInformation("Ordem agendada criada. Ordem={IdOrdem} Status={Status} DataAgendamento={DataAgendamento}", result!.IdOrdem, result!.Status, result!.DataAgendamento);
        return result!;
    }
}
