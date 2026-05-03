using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;

namespace CaseGig.Application.Operations;

public interface IOrdemOperationHandler
{
    TipoOperacao Operation { get; }

    Ordem CreateImmediate(Cliente cliente, Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateTime agora);

    Ordem CreateScheduled(Cliente cliente, Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateOnly dataAgendamento, DateTime agora);

    void Process(Ordem ordem, Cliente cliente, Fundo fundo, Posicao? posicao, DateTime agora);
}

