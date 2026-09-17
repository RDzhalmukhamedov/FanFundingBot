using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace FanFundingBot.Application.Common.Errors;

[ExcludeFromCodeCoverage]
public class ValidationError : Error
{
    public string Code { get; }

    public ValidationError(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
