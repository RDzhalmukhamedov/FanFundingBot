namespace FanFundingBot.Domain.Exceptions;

public class AddonNotEditModeException : DomainException
{
    public AddonNotEditModeException() : base() { }

    public AddonNotEditModeException(string message) : base(message) { }

    public AddonNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }
}