using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Domain.Interfaces;

public interface IOrderExternalStorage
{
    Task SaveOrderAsync(Order order);
    Task DeleteOrderAsync(Guid orderId);
}