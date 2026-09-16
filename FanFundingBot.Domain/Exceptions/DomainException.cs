namespace FanFundingBot.Domain.Exceptions;

/// <summary>
/// Базовое исключение доменного слоя: нарушение инварианта агрегата.
/// Application конвертирует его в Result.Fail, callers проверяют результат, а не ловят исключения.
/// Необязательный <see cref="Code"/> позволяет TransactionBehavior вернуть типизированный IError;
/// без кода — fallback на plain Error (массовая миграция throw-сайтов на коды отложена).
/// </summary>
public class DomainException : Exception
{
    public string? Code { get; }

    public DomainException() : base() { }

    public DomainException(string message) : base(message) { }

    public DomainException(string message, string? code) : base(message) => Code = code;

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }

    public DomainException(string message, string? code, Exception innerException)
        : base(message, innerException) => Code = code;
}
