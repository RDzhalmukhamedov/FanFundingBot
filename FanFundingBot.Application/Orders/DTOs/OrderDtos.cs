using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Application.Orders.DTOs;

[ExcludeFromCodeCoverage]
public sealed record OrderPositionDto(Guid AddonId, string AddonName, decimal Price, int Quantity);

[ExcludeFromCodeCoverage]
public sealed record OrderDto(
    Guid Id,
    long UserId,
    Guid ProjectId,
    string ProjectName,
    string Phone,
    DeliveryMethod DeliveryMethod,
    string? Address,
    OrderStatus Status,
    DateTime CreatedAt,
    List<OrderPositionDto> Positions,
    decimal Total);
