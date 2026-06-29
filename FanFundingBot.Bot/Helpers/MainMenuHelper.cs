using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Keyboards;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Helpers;

public class MainMenuHelper
{
    private readonly IAdminService _adminService;

    public MainMenuHelper(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<ReplyKeyboardMarkup> CreateKeyboardAsync(long userId)
    {
        var isAdmin = await _adminService.IsAdminAsync(userId);
        return MainMenuKeyboard.Create(isAdmin);
    }

    public async Task SendReturnToMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct)
    {
        var keyboard = await CreateKeyboardAsync(userId);
        await botClient.SendMessage(
            chatId,
            "Возвращаюсь в главное меню.",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }
}
