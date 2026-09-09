using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace FanFundingBot.Application.Common.Errors;

/// <summary>
/// Сущность не найдена. Message сохраняет исторический русский текст,
/// поэтому Bot/Web, рендерящие Errors[0].Message, не ломаются.
/// Тип позволяет делать match по коду вместо сравнения строк.
/// </summary>
[ExcludeFromCodeCoverage]
public class NotFoundError : Error
{
    public string Code { get; }

    public NotFoundError(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
