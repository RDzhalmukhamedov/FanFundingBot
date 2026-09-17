using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace FanFundingBot.Application.Common.Errors;

/// <summary>
/// Хелперы для Presentation (Bot/Web): match по типу вместо сравнения строк.
/// Тексты не меняются — <see cref="UserMessage"/> возвращает исторический Message.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ResultErrorExtensions
{
    public static string UserMessage(this ResultBase result) =>
        result.Errors.Count > 0 ? result.Errors[0].Message : "Произошла ошибка. Попробуйте позже.";

    public static bool IsNotFound(this ResultBase result) =>
        result.HasError<NotFoundError>();

    public static bool IsForbidden(this ResultBase result) =>
        result.HasError<ForbiddenError>();

    public static bool IsConflict(this ResultBase result) =>
        result.HasError<ConflictError>();
}
