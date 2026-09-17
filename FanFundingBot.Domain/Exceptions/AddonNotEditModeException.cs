namespace FanFundingBot.Domain.Exceptions;

public class AddonNotEditModeException : DomainException
{
    public AddonNotEditModeException() : base() { }

    public AddonNotEditModeException(string message) : base(message) { }

    public AddonNotEditModeException(string message, string? code) : base(message, code) { }

    public AddonNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }

    public AddonNotEditModeException(string message, string? code, Exception innerException)
        : base(message, code, innerException) { }
}
