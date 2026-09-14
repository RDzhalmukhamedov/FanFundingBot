using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.Application.Common.Models;

[ExcludeFromCodeCoverage]
public sealed record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
