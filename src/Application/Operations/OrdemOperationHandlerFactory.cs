using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

namespace CaseGig.Application.Operations;

public sealed class OrdemOperationHandlerFactory
{
    private readonly IReadOnlyDictionary<TipoOperacao, IOrdemOperationHandler> _handlersByOperation;

    public OrdemOperationHandlerFactory(IEnumerable<IOrdemOperationHandler> handlers)
    {
        _handlersByOperation = handlers.ToDictionary(x => x.Operation);
    }

    public IOrdemOperationHandler Get(TipoOperacao operation)
    {
        if (_handlersByOperation.TryGetValue(operation, out var handler))
        {
            return handler;
        }

        throw new BusinessRuleException("Tipo de operação inválido.");
    }
}

