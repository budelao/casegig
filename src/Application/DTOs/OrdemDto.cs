using CaseGig.Domain.Enums;

namespace CaseGig.Application.DTOs;

public sealed record OrdemDto(
    Guid IdOrdem,
    Guid IdCliente,
    Guid IdFundo,
    TipoOperacao TipoOperacao,
    StatusOrdem Status,
    decimal QuantidadeCotas,
    DateTime DataCriacao,
    DateTime? DataAgendamento,
    DateTime? DataProcessamento
);
