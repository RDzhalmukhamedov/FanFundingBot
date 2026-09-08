using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Application.Projects.DTOs;

[ExcludeFromCodeCoverage]
public sealed record AddonDto(Guid Id, string Name, string? Description, decimal Price, bool HasOrders = false);

[ExcludeFromCodeCoverage]
public sealed record ProjectMemberDto(long UserId, ProjectRole Role);

[ExcludeFromCodeCoverage]
public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    int StatusId,
    string StatusName,
    bool CanEditOrders,
    DateTime? CreatedAt,
    List<AddonDto> Addons,
    List<ProjectMemberDto> Members);

[ExcludeFromCodeCoverage]
public sealed record ProjectStatusDto(int Id, string DisplayName, int Order, bool CanEditOrders);

[ExcludeFromCodeCoverage]
public sealed record NewAddonItem(string Name, string Description, decimal Price);
