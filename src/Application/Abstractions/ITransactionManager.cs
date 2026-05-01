namespace CaseGig.Application.Abstractions;

public interface ITransactionManager
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}
