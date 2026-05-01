using CaseGig.Domain.Entities;

namespace CaseGig.Application.Abstractions;

public interface IClienteRepository
{
    Task<Cliente?> GetByIdAsync(Guid idCliente, CancellationToken cancellationToken);
}
