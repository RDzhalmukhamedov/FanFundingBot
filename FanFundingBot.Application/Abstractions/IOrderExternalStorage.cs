using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Application.Abstractions;

public interface IOrderExternalStorage
{
    Task SaveOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
