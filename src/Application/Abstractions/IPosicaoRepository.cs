using CaseGig.Domain.Entities;

namespace CaseGig.Application.Abstractions;

public interface IPosicaoRepository
{
    Task<Posicao?> GetByIdAsync(Guid idCliente, Guid idFundo, CancellationToken cancellationToken);
    Task<IReadOnlyList<Posicao>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken);
}
