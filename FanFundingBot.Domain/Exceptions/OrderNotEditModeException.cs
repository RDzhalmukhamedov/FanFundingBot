namespace FanFundingBot.Domain.Exceptions;

public class OrderNotEditModeException : DomainException
{
    public OrderNotEditModeException() : base() { }

    public OrderNotEditModeException(string message) : base(message) { }

    public OrderNotEditModeException(string message, string? code) : base(message, code) { }

    public OrderNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }

    public OrderNotEditModeException(string message, string? code, Exception innerException)
        : base(message, code, innerException) { }
}
