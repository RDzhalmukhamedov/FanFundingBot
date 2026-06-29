using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Handlers;

public class CallbackQueryHandler : ICallbackQueryHandler
{
    private readonly IAdminService _adminService;
    private readonly Dictionary<string, ICallbackCommandHandler> _handlers;

    public CallbackQueryHandler(
        IAdminService adminService,
        IEnumerable<ICallbackCommandHandler> handlers)
    {
        _adminService = adminService;
        _handlers = handlers
            .SelectMany(h => h.Commands.Select(cmd => (cmd, handler: h)))
            .ToDictionary(x => x.cmd, x => x.handler);
    }

    public async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken ct)
    {
        var userId = query.From.Id;
        var data = query.Data!;

        try
        {
            var (command, _) = CallbackData.Parse(data);

            if (!_handlers.TryGetValue(command, out var handler))
                return;

            if (handler.RequireAdmin && !await _adminService.IsAdminAsync(userId))
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