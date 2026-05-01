using CaseGig.Domain.Enums;

namespace CaseGig.Api.Contracts;

public sealed record CriarOrdemAgendamentoRequest(
    Guid IdCliente,
    Guid IdFundo,
    TipoOperacao TipoOperacao,
    decimal QuantidadeCotas,
    DateOnly DataAgendamento
);
