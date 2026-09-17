using FanFundingBot.Bot.Abstractions;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Handlers;

public class UpdateHandler : IUpdateHandler
{
    private readonly ICommandHandler _commandHandler;
    private readonly ICallbackQueryHandler _callbackQueryHandler;
    private readonly IContactHandler _contactHandler;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        ICommandHandler commandHandler,
        ICallbackQueryHandler callbackQueryHandler,
        IContactHandler contactHandler,
        ILogger<UpdateHandler> logger)
    {
        _commandHandler = commandHandler;
        _callbackQueryHandler = callbackQueryHandler;
        _contactHandler = contactHandler;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageAsync(botClient, update.Message!, ct);
                    break;

                case UpdateType.CallbackQuery:
                    await _callbackQueryHandler.HandleCallbackQueryAsync(botClient, update.CallbackQuery!, ct);
                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update handling failed for update {UpdateId}", update.Id);
            if (update.Message?.Chat is { } chat)
            {
                try
                {
                    await botClient.SendMessage(
                        chat.Id,
                        "Произошла внутренняя ошибка. Пожалуйста, попробуйте позже.",
                        cancellationToken: ct);
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        if (message.Text == null && message.Contact == null)
            return;

        if (message.Contact != null)
        {
            await _contactHandler.HandleContactAsync(botClient, message, ct);
            return;
        }

        await _commandHandler.HandleCommandAsync(botClient, message, ct);
    }
}
