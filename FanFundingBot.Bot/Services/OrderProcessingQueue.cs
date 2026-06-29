using FanFundingBot.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace FanFundingBot.Bot.Services;

public record OrderSaveRequest(
    long UserId,
    long ChatId,
    int MessageId,
    bool IsEditing,
    Guid? EditingOrderId,
    Guid ProjectId,
    HashSet<Guid> AddonIds,
    Dictionary<Guid, int> AddonQuantities,
    string Phone,
    DeliveryMethod DeliveryMethod,
    string? Address
);

public interface IOrderProcessingQueue
{
    ValueTask EnqueueAsync(OrderSaveRequest request, CancellationToken ct);
    ValueTask<OrderSaveRequest> TryDequeueAsync(CancellationToken ct);
}

public class OrderProcessingQueue : IOrderProcessingQueue
{
    private readonly Channel<OrderSaveRequest> _channel;
    private readonly ILogger<OrderProcessingQueue> _logger;

    public OrderProcessingQueue(ILogger<OrderProcessingQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<OrderSaveRequest>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
    }

    public async ValueTask EnqueueAsync(OrderSaveRequest request, CancellationToken ct)
    {
        if (!_channel.Writer.TryWrite(request))
        {
            _logger.LogWarning(
                "OrderProcessingQueue: channel full, dropping oldest order. " +
                "UserId={UserId}, ProjectId={ProjectId}", request.UserId, request.ProjectId);

            await _channel.Writer.WriteAsync(request, ct);
        }
    }

    public ValueTask<OrderSaveRequest> TryDequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}