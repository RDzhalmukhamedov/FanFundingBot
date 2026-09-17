using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Abstractions;

public record ProjectEditForm(
    string Text,
    InlineKeyboardMarkup Keyboard
);

public interface IProjectEditFormBuilder
{
    Task<ProjectEditForm?> BuildAsync(Guid projectId, string? prependText = null);
}