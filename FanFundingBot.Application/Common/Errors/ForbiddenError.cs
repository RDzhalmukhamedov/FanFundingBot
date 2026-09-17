using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace FanFundingBot.Application.Common.Errors;

/// <summary>
/// Нет прав на операцию. Message сохраняет исторический текст "Нет прав.".
/// </summary>
[ExcludeFromCodeCoverage]
public class ForbiddenError : Error
{
    public string Code { get; }

    public ForbiddenError(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
