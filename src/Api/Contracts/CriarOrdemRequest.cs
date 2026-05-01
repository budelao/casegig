using CaseGig.Domain.Enums;

namespace CaseGig.Api.Contracts;

public sealed record CriarOrdemRequest(
    Guid IdCliente,
    Guid IdFundo,
    TipoOperacao TipoOperacao,
    decimal? ValorAporte,
    decimal? QuantidadeCotas
);
