using System.Net;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class NotificationService : INotificationPort
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfiguration _config;
    private readonly ILogger<NotificationService> _logger;
    private static readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public NotificationService(ITelegramBotClient botClient, BotConfiguration config, ILogger<NotificationService> logger)
    {
        _botClient = botClient;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyProjectStatusChangedAsync(
        IReadOnlyList<long> userIds, string projectName, string newStatus, CancellationToken ct)
    {
        var message = $"📋 <b>Статус проекта «{Esc(projectName)}» обновлён</b>\n\n" +
                      $"Новый статус: <b>{GetStatusEmoji(newStatus)} {Esc(newStatus)}</b>";

        await SendWithRateLimitAsync(userIds, message, ParseMode.Html, ct);
    }

    public async Task BroadcastToProjectAsync(
        IReadOnlyList<long> userIds, string message, CancellationToken ct)
    {
        await SendWithRateLimitAsync(userIds, Esc(message), ParseMode.Html, ct);
    }

    public async Task NotifyQuestionAnsweredAsync(long userId, string questionText, string answer, CancellationToken ct)
    {
        try
        {
            var message = $"✉️ <b>Ответ на ваш вопрос</b>\n\n" +
                          $"<b>Ваш вопрос:</b> {Esc(questionText)}\n\n" +
                          $"<b>Ответ:</b> {Esc(answer)}";

            await _botClient.SendMessage(
                chatId: userId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Ignore users who blocked the bot
            _logger.LogDebug(ex, "Skipping notify for blocked user {UserId} (question answered)", userId);
        }
    }

    public async Task NotifyOrderStatusChangedAsync(
        long userId, string projectName, Domain.Enums.OrderStatus newStatus, CancellationToken ct)
    {
        try
        {
            var message = $"📦 <b>Статус вашего заказа обновлён</b>\n\n" +
                          $"Проект: <b>{Esc(projectName)}</b>\n" +
                          $"Новый статус: <b>{GetOrderStatusText(newStatus)}</b>";

            await _botClient.SendMessage(
                chatId: userId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Ignore users who blocked the bot
            _logger.LogDebug(ex, "Skipping notify for blocked user {UserId} (order status)", userId);
        }
    }

    public async Task SendDirectMessageAsync(long userId, string message, CancellationToken ct)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: userId,
                text: Esc(message),
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Ignore users who blocked the bot
            _logger.LogDebug(ex, "Skipping direct message for blocked user {UserId}", userId);
        }
    }

    private async Task SendWithRateLimitAsync(
        IReadOnlyList<long> userIds, string text, ParseMode parseMode, CancellationToken ct)
    {
        await _sendSemaphore.WaitAsync(ct);
        try
        {
            foreach (var userId in userIds)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: userId,
                        text: text,
                        parseMode: parseMode,
                        cancellationToken: ct);

                    if (_config.SendDelayMs > 0)
                        await Task.Delay(_config.SendDelayMs, ct);
                }
                catch (Exception ex)
                {
                    // Ignore users who blocked the bot
                    _logger.LogDebug(ex, "Skipping broadcast for blocked user {UserId}", userId);
                }
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private static string GetOrderStatusText(Domain.Enums.OrderStatus status)
    {
        return status switch
        {
            Domain.Enums.OrderStatus.New => "🆕 Новый",
            Domain.Enums.OrderStatus.Confirmed => "✅ Подтверждён",
            Domain.Enums.OrderStatus.Completed => "✔️ Выполнен",
            Domain.Enums.OrderStatus.Cancelled => "❌ Отменён",
            _ => status.ToString()
        };
    }

    private static string GetStatusEmoji(string status)
    {
        return status switch
        {
            "Запланирован" => "📅",
            "Перевод" => "🔄",
            "Вычитка" => "📝",
            "Подготовка макетов" => "🎨",
            "Печать" => "🖨️",
            "Сортировка" => "📦",
            "Отправка" => "🚚",
            "Завершён" => "✅",
            "Приостановлен" => "⏸️",
            _ => "❓"
        };
    }

    // Сообщения уходят с ParseMode.Html: пользовательский/менеджерский ввод экранируем,
    // иначе случайный '<' ломает разметку — Telegram вернёт 400, а catch выше запишет
    // это как «пользователь заблокировал бота» и сообщение молча потеряется.
    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
