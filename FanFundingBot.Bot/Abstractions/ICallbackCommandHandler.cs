using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

/// <summary>
/// Handles a group of callback commands.
/// </summary>
public interface ICallbackCommandHandler
{
    /// <summary>Callback commands supported by this handler.</summary>
    string[] Commands { get; }

    /// <summary>Whether this handler requires admin privileges.</summary>
    bool RequireAdmin { get; }

    Task HandleAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
}