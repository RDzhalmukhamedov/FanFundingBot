using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.App.Services;

public class BackgroundOrderProcessor : BackgroundService
{
    private readonly IOrderProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundOrderProcessor> _logger;

    public BackgroundOrderProcessor(
        IOrderProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundOrderProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundOrderProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await _queue.TryDequeueAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var orderFlowService = scope.ServiceProvider.GetRequiredService<IOrderFlowService>();
                var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

                try
                {
                    if (request.IsEditing)
                    {
                        await orderFlowService.UpdateOrderAsync(
                            request.EditingOrderId!.Value, request.ProjectId, request.AddonIds,
                            request.AddonQuantities, request.Phone, request.DeliveryMethod, request.Address);

                        await botClient.SendMessage(
                            request.ChatId,
                            "✅ <b>Заказ обновлён!</b>\n\nВаш заказ успешно изменён.",
                            parseMode: ParseMode.Html,
                            cancellationToken: stoppingToken);
                    }
                    else
                    {
                        var order = await orderFlowService.CreateOrderAsync(
                            request.UserId, request.ProjectId, request.AddonIds,
                            request.AddonQuantities, request.Phone, request.DeliveryMethod, request.Address);

                        await botClient.SendMessage(
                            request.ChatId,
                            $"✅ <b>Заказ успешно оформлен!</b>\n\nНомер заказа: <code>{order.Id:N}</code>\n\nМы свяжемся с вами в ближайшее время.",
                            parseMode: ParseMode.Html,
                            cancellationToken: stoppingToken);
                    }
                }
                catch (Core.Exceptions.OrderSaveException)
                {
                    await botClient.SendMessage(
                        request.ChatId,
                        "Не удалось оформить заказ. Пожалуйста, попробуйте позже.",
                        cancellationToken: stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background order processing failed");
            }
        }

        _logger.LogInformation("BackgroundOrderProcessor stopped");
    }
}
