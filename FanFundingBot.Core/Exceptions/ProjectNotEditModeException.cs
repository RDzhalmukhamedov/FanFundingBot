namespace FanFundingBot.Core.Exceptions;

public class ProjectNotEditModeException : Exception
{
    public ProjectNotEditModeException() : base() { }

    public ProjectNotEditModeException(string message) : base(message) { }

    public ProjectNotEditModeException(string message, Exception innerException)
        : base(message, innerException) { }
}