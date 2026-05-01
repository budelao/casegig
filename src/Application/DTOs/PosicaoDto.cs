namespace CaseGig.Application.DTOs;

public sealed record PosicaoDto(
    Guid IdCliente,
    Guid IdFundo,
    decimal QuantidadeCotas
);
