using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.Application.Questions.DTOs;

[ExcludeFromCodeCoverage]
public sealed record QuestionDto(
    Guid Id,
    string Text,
    long UserId,
    string? Username,
    string? FirstName,
    DateTime CreatedAt,
    string? Answer);
