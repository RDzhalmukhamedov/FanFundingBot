using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class ProjectEditFormBuilder : IProjectEditFormBuilder
{
    private readonly IAdminService _adminService;
    private readonly IAddonRepository _addonRepository;

    public ProjectEditFormBuilder(IAdminService adminService, IAddonRepository addonRepository)
    {
        _adminService = adminService;
        _addonRepository = addonRepository;
    }

    public async Task<ProjectEditForm> BuildAsync(Guid projectId, string? prependText = null)
    {
        var project = await _adminService.GetProjectAsync(projectId)
            ?? throw new InvalidOperationException("Project not found");

        var addons = await _addonRepository.GetByProjectIdAsync(projectId);
        var addonSummary = addons.Count == 0
            ? "Нет дополнений"
            : string.Join("\n", addons.Select(a => $"  • {a.Name} — {a.Price:F2}₽"));

        var statusName = (await _adminService.GetAllStatusesAsync())
            .FirstOrDefault(s => s.Id == project.Status.Id)?.DisplayName ?? "—";

        var text = $"✏️ <b>Редактирование: {project.Name}</b>\n\n" +
                   $"<b>Описание:</b> {project.Description ?? "—"}\n" +
                   $"<b>Статус:</b> {statusName}\n\n" +
                   $"<b>Дополнения:</b>\n{addonSummary}";

        if (prependText != null)
            text = $"{prependText}\n\n{text}";

        var editButtons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("📝 Изменить название", CallbackData.Build("admin_edit_project_name", projectId)) },
            new[] { InlineKeyboardButton.WithCallbackData("📝 Изменить описание", CallbackData.Build("admin_edit_project_desc", projectId)) },
            new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить дополнение", CallbackData.Build("admin_edit_add_addon", projectId)) }
        };

        foreach (var addon in addons)
        {
            editButtons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"✏️ {addon.Name}", CallbackData.Build("admin_edit_addon", addon.Id)),
                InlineKeyboardButton.WithCallbackData($"❌ {addon.Name} (удалить)", CallbackData.Build("admin_del_addon", addon.Id))
            });
        }

        editButtons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Закрыть", "admin_cancel") });

        return new ProjectEditForm(text, new InlineKeyboardMarkup(editButtons));
    }
}