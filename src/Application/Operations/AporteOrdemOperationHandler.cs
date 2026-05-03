using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Services;

namespace CaseGig.Application.Operations;

public sealed class AporteOrdemOperationHandler : IOrdemOperationHandler
{
    private readonly OrdemService _ordemService;
    private readonly OrdemProcessamentoService _processamentoService;

    public AporteOrdemOperationHandler(OrdemService ordemService, OrdemProcessamentoService processamentoService)
    {
        _ordemService = ordemService;
        _processamentoService = processamentoService;
    }

    public TipoOperacao Operation => TipoOperacao.APORTE;

    public Ordem CreateImmediate(Cliente cliente, Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateTime agora)
    {
        return _ordemService.CriarOrdemAportePorCotas(cliente, fundo, quantidadeCotas, agora);
    }

    public Ordem CreateScheduled(Cliente cliente, Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateOnly dataAgendamento, DateTime agora)
    {
        return _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, quantidadeCotas, dataAgendamento, agora);
    }

    public void Process(Ordem ordem, Cliente cliente, Fundo fundo, Posicao? posicao, DateTime agora)
    {
        _processamentoService.PrepararParaProcessamento(ordem, agora);
        _processamentoService.ProcessarOrdemAporte(ordem, cliente, fundo, posicao);
        _processamentoService.Concluir(ordem, agora);
    }
}

