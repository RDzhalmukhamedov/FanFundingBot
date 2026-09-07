namespace FanFundingBot.Domain.Exceptions;

/// <summary>
/// Базовое исключение доменного слоя: нарушение инварианта агрегата.
/// Application конвертирует его в Result.Fail, callers проверяют результат, а не ловят исключения.
/// </summary>
public class DomainException : Exception
{
    public DomainException() : base() { }

    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
