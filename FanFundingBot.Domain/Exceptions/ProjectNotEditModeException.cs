namespace FanFundingBot.Domain.Exceptions;

public class ProjectNotEditModeException : DomainException
{
    public ProjectNotEditModeException() : base() { }

    public ProjectNotEditModeException(string message) : base(message) { }

    public ProjectNotEditModeException(string message, string? code) : base(message, code) { }

    public ProjectNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }

    public ProjectNotEditModeException(string message, string? code, Exception innerException)
        : base(message, code, innerException) { }
}
