namespace FanFundingBot.Core.Exceptions;

public class OrderSaveException : Exception
{
    public OrderSaveException(string message) : base(message) { }

    public OrderSaveException(string message, Exception innerException)
        : base(message, innerException) { }
}