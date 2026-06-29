using FanFundingBot.Core.Entities;

namespace FanFundingBot.Core.Interfaces;

public interface IOrderExternalStorage
{
    Task SaveOrderAsync(Order order);
    Task DeleteOrderAsync(Guid orderId);
}