using CaseGig.Domain.Enums;

namespace CaseGig.Api.Models.Requests;

public sealed record CriarOrdemRequest(
    Guid IdCliente,
    Guid IdFundo,
    TipoOperacao TipoOperacao,
    decimal QuantidadeCotas
);
