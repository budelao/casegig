using CaseGig.Domain.Enums;

namespace CaseGig.Application.DTOs;

public sealed record CriarOrdemAgendamentoRequestDto(
    Guid IdCliente,
    Guid IdFundo,
    TipoOperacao TipoOperacao,
    decimal QuantidadeCotas,
    DateOnly DataAgendamento
);
