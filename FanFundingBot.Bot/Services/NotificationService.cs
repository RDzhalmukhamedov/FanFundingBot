using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class NotificationService : INotificationPort
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfiguration _config;
    private static readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public NotificationService(ITelegramBotClient botClient, BotConfiguration config)
    {
        _botClient = botClient;
        _config = config;
    }

    public async Task NotifyProjectStatusChangedAsync(
        IReadOnlyList<long> userIds, string projectName, string newStatus, CancellationToken ct)
    {
        var message = $"📋 <b>Статус проекта «{projectName}» обновлён</b>\n\n" +
                      $"Новый статус: <b>{GetStatusEmoji(newStatus)} {newStatus}</b>";

        await SendWithRateLimitAsync(userIds, message, ParseMode.Html, ct);
    }

    public async Task BroadcastToProjectAsync(
        IReadOnlyList<long> userIds, string message, CancellationToken ct)
    {
        await SendWithRateLimitAsync(userIds, message, ParseMode.Html, ct);
    }

    public async Task NotifyQuestionAnsweredAsync(long userId, string questionText, string answer, CancellationToken ct)
    {
        try
        {
            var message = $"✉️ <b>Ответ на ваш вопрос</b>\n\n" +
                          $"<b>Ваш вопрос:</b> {questionText}\n\n" +
                          $"<b>Ответ:</b> {answer}";

            await _botClient.SendMessage(
                chatId: userId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch
        {
            // Ignore users who blocked the bot
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
                catch
                {
                    // Ignore users who blocked the bot
                }
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
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
}
