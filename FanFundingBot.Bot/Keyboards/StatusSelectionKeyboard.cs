using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Bot.Helpers;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

[ExcludeFromCodeCoverage]
public static class StatusSelectionKeyboard
{
    private const int PageSize = 6;

    public static (InlineKeyboardMarkup Keyboard, int TotalPages) Create(
        List<ProjectStatusDto> allStatuses,
        Guid projectId,
        int page)
    {
        var totalPages = (int)Math.Ceiling((double)allStatuses.Count / PageSize);
        if (totalPages < 1) totalPages = 1;

        var pageStatuses = allStatuses
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var status in pageStatuses)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{status.DisplayName} ({(status.CanEditOrders ? "✅ можно ред." : "❌ нельзя ред.")})",
                    CallbackData.Build("admin_status", projectId, status.Id))
            });
        }

        var navRow = new List<InlineKeyboardButton>();
        if (page > 1)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("◀️ Назад", CallbackData.Build("admin_status_page", projectId, page - 1)));
        }

        navRow.Add(InlineKeyboardButton.WithCallbackData(
            $"{page}/{totalPages}", CallbackData.Build("admin_status_page", projectId, "nop")));

        if (page < totalPages)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("Вперёд ▶️", CallbackData.Build("admin_status_page", projectId, page + 1)));
        }

        buttons.Add(navRow);

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("➕ Добавить статус", CallbackData.Build("admin_add_status", projectId))
        });

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("❌ Отмена", "admin_cancel")
        });

        return (new InlineKeyboardMarkup(buttons), totalPages);
    }
}
