using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Models;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class NotificationService : INotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserRepository _userRepository;
    private readonly BotConfiguration _config;
    private static readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public NotificationService(ITelegramBotClient botClient, IUserRepository userRepository, BotConfiguration config)
    {
        _botClient = botClient;
        _userRepository = userRepository;
        _config = config;
    }

    public async Task NotifyProjectStatusUpdateAsync(Guid projectId, string projectName, string newStatus)
    {
        var users = await _userRepository.GetSubscribedToProjectAsync(projectId);

        var message = $"📋 <b>Статус проекта «{projectName}» обновлён</b>\n\n" +
                      $"Новый статус: <b>{GetStatusEmoji(newStatus)} {newStatus}</b>";

        await SendWithRateLimitAsync(users, message);
    }

    public async Task BroadcastToProjectAsync(Guid projectId, string text)
    {
        var users = await _userRepository.GetSubscribedToProjectAsync(projectId);

        await SendWithRateLimitAsync(users, text, ParseMode.Html);
    }

    public async Task NotifyQuestionAnsweredAsync(long userId, string questionText, string answer)
    {
        try
        {
            var message = $"✉️ <b>Ответ на ваш вопрос</b>\n\n" +
                          $"<b>Ваш вопрос:</b> {questionText}\n\n" +
                          $"<b>Ответ:</b> {answer}";

            await _botClient.SendMessage(
                chatId: userId,
                text: message,
                parseMode: ParseMode.Html);
        }
        catch
        {
            // Ignore users who blocked the bot
        }
    }

    private async Task SendWithRateLimitAsync(List<Domain.Entities.User> users, string text, ParseMode? parseMode = null)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            foreach (var user in users)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: user.Id,
                        text: text,
                        parseMode: parseMode ?? ParseMode.Html,
                        cancellationToken: default);

                    if (_config.SendDelayMs > 0)
                        await Task.Delay(_config.SendDelayMs);
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
