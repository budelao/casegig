using CaseGig.Domain.Entities;

namespace CaseGig.Application.Abstractions;

public interface IFundoRepository
{
    Task<Fundo?> GetByIdAsync(Guid idFundo, CancellationToken cancellationToken);
}
