using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;

namespace CaseGig.Application.UseCases;

public sealed class CriarOrdemUseCase
{
    private readonly ITransactionManager _transactionManager;
    private readonly IClienteRepository _clienteRepository;
    private readonly IFundoRepository _fundoRepository;
    private readonly IPosicaoRepository _posicaoRepository;
    private readonly IOrdemRepository _ordemRepository;
    private readonly OrdemService _ordemService;
    private readonly OrdemProcessamentoService _processamentoService;

    public CriarOrdemUseCase(
        ITransactionManager transactionManager,
        IClienteRepository clienteRepository,
        IFundoRepository fundoRepository,
        IPosicaoRepository posicaoRepository,
        IOrdemRepository ordemRepository,
        OrdemService ordemService,
        OrdemProcessamentoService processamentoService)
    {
        _transactionManager = transactionManager;
        _clienteRepository = clienteRepository;
        _fundoRepository = fundoRepository;
        _posicaoRepository = posicaoRepository;
        _ordemRepository = ordemRepository;
        _ordemService = ordemService;
        _processamentoService = processamentoService;
    }

    public async Task<CriarOrdemResultDto> ExecuteAsync(CriarOrdemRequestDto request, DateTime agora, CancellationToken cancellationToken)
    {
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
                TipoOperacao.APORTE => CriarOrdemAporte(cliente, fundo, request, agora),
                TipoOperacao.RESGATE => CriarOrdemResgate(cliente, fundo, posicao, request, agora),
                _ => throw new BusinessRuleException("Tipo de operação inválido.")
            };

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

        return result!;
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
}
