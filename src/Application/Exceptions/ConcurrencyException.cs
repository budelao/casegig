namespace CaseGig.Application.Exceptions;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
