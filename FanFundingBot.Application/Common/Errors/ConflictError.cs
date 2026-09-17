using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace FanFundingBot.Application.Common.Errors;

/// <summary>
/// Конфликт состояния (дубль заказа, недоступный проект, занятый addon).
/// Message сохраняет исторические тексты.
/// </summary>
[ExcludeFromCodeCoverage]
public class ConflictError : Error
{
    public string Code { get; }

    public ConflictError(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
