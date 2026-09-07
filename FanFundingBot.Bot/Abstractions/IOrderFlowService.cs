using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Bot.Abstractions;

public interface IOrderFlowService
{
    string? ValidatePhone(string phone);
    string? ValidateAddress(string address);
    Task<Order> CreateOrderAsync(long userId, Guid projectId, HashSet<Guid> addonIds, Dictionary<Guid, int> addonQuantities, string phone, DeliveryMethod deliveryMethod, string? address);
    Task UpdateOrderAsync(Guid orderId, Guid projectId, HashSet<Guid> addonIds, Dictionary<Guid, int> addonQuantities, string phone, DeliveryMethod deliveryMethod, string? address);
    Task DeleteOrderAsync(Guid orderId);
    Task<Project?> GetProjectWithAddonsAsync(Guid projectId);
    Task<List<Addon>> GetAddonsPagedAsync(Guid projectId, int page, int pageSize);
    Task<int> GetAddonCountAsync(Guid projectId);
    Task<Order?> GetLatestOrderAsync(long userId);
    Task<Order?> GetLatestOrderWithAddonsAsync(long userId);
}
