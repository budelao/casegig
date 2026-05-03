using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;

namespace CaseGig.Application.Operations;

public sealed class ResgateOrdemOperationHandler : IOrdemOperationHandler
{
    private readonly OrdemService _ordemService;
    private readonly OrdemProcessamentoService _processamentoService;

    public ResgateOrdemOperationHandler(OrdemService ordemService, OrdemProcessamentoService processamentoService)
    {
        _ordemService = ordemService;
        _processamentoService = processamentoService;
    }

    public TipoOperacao Operation => TipoOperacao.RESGATE;

    public Ordem CreateImmediate(Cliente cliente, Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateTime agora)
    {
        return _ordemService.CriarOrdemResgate(cliente, fundo, posicao, quantidadeCotas, agora);
    }

    public Ordem CreateScheduled(Cliente cliente, Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateOnly dataAgendamento, DateTime agora)
    {
        return _ordemService.CriarOrdemAgendadaResgate(cliente, fundo, posicao, quantidadeCotas, dataAgendamento, agora);
    }

    public void Process(Ordem ordem, Cliente cliente, Fundo fundo, Posicao? posicao, DateTime agora)
    {
        if (posicao is null)
        {
            throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
        }

        _processamentoService.PrepararParaProcessamento(ordem, agora);
        _processamentoService.ProcessarOrdemResgate(ordem, cliente, fundo, posicao);
        _processamentoService.Concluir(ordem, agora);
    }
}

