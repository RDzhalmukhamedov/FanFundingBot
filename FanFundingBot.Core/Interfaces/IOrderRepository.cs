using FanFundingBot.Core.Entities;

namespace FanFundingBot.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task<Order?> GetByIdWithAddonsAsync(Guid id);
    Task<List<Order>> GetByUserAsync(long userId);
    Task<List<Order>> GetByUserWithAddonsAsync(long userId);
    Task<List<Order>> GetByProjectAsync(Guid projectId);
    Task<List<Order>> GetByProjectAndUserAsync(Guid projectId, long userId);
    Task AddAsync(Order order);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Guid id);
    Task DeleteOrderAddonsAsync(Guid orderId);
}
