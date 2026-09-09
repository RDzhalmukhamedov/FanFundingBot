namespace FanFundingBot.Domain.Exceptions;

public class ProjectNotEditModeException : DomainException
{
    public ProjectNotEditModeException() : base() { }

    public ProjectNotEditModeException(string message) : base(message) { }

    public ProjectNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }
}