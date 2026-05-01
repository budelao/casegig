using CaseGig.Domain.Enums;

namespace CaseGig.Application.DTOs;

public sealed record CriarOrdemRequestDto(
    Guid IdCliente,
    Guid IdFundo,
    TipoOperacao TipoOperacao,
    decimal? ValorAporte,
    decimal? QuantidadeCotas
);
