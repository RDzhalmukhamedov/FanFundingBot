namespace FanFundingBot.Domain.Exceptions;

public class OrderNotEditModeException : DomainException
{
    public OrderNotEditModeException() : base() { }

    public OrderNotEditModeException(string message) : base(message) { }

    public OrderNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }
}