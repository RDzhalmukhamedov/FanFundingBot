using FluentResults;
using FanFundingBot.Application.Common.Errors;

namespace FanFundingBot.App.Components;

/// <summary>
/// Маппинг <c>Result</c> → текст для Blazor (без HTTP-кодов).
/// Успех → null. Тексты 1:1 с Bot/Web (<c>UserMessage</c>).
/// </summary>
public static class BlazorResult
{
    public static string? Message(ResultBase result) =>
        result.IsSuccess ? null : result.UserMessage();
}
