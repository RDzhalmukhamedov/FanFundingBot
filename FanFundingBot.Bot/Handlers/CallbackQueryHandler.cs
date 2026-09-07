using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Handlers;

public class CallbackQueryHandler : ICallbackQueryHandler
{
    private readonly IAuthorizationService _authorization;
    private readonly TelegramUserContext _userContext;
    private readonly Dictionary<string, ICallbackCommandHandler> _handlers;

    public CallbackQueryHandler(
        IAuthorizationService authorization,
        TelegramUserContext userContext,
        IEnumerable<ICallbackCommandHandler> handlers)
    {
        _authorization = authorization;
        _userContext = userContext;
        _handlers = handlers
            .SelectMany(h => h.Commands.Select(cmd => (cmd, handler: h)))
            .ToDictionary(x => x.cmd, x => x.handler);
    }

    public async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken ct)
    {
        var userId = query.From.Id;
        var data = query.Data!;
        _userContext.UserId = userId;

        try
        {
            var (command, _) = CallbackData.Parse(data);

            if (!_handlers.TryGetValue(command, out var handler))
                return;

            if (handler.RequireAdmin && !await _authorization.CanCreateProjectAsync(userId))
            {
                await botClient.AnswerCallbackQuery(query.Id, "Нет прав.", showAlert: true, cancellationToken: ct);
                return;
            }

            await handler.HandleAsync(botClient, query, userId, data, ct);
        }
        catch (Exception)
        {
            await botClient.AnswerCallbackQuery(
                query.Id,
                "Произошла ошибка. Попробуйте позже.",
                cancellationToken: ct);
        }

        await botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);
    }
}