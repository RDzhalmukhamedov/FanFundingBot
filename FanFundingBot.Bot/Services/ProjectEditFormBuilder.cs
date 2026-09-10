using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class ProjectEditFormBuilder : IProjectEditFormBuilder
{
    private readonly IMediator _mediator;

    public ProjectEditFormBuilder(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ProjectEditForm?> BuildAsync(Guid projectId, string? prependText = null)
    {
        var projectResult = await _mediator.Send(new GetProjectByIdQuery(projectId));
        if (projectResult.IsFailed)
            return null;

        var project = projectResult.Value;

        var statusesResult = await _mediator.Send(new GetProjectStatusesQuery());
        var statuses = statusesResult.IsSuccess ? statusesResult.Value : new List<ProjectStatusDto>();

        var addonSummary = project.Addons.Count == 0
            ? "Нет дополнений"
            : string.Join("\n", project.Addons.Select(a => $"  • {a.Name} — {a.Price:F2}₽" + (a.HasOrders ? " 🔒 используется" : "")));

        var statusName = statuses
            .FirstOrDefault(s => s.Id == project.StatusId)?.DisplayName ?? "—";

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

        foreach (var addon in project.Addons)
        {
            if (addon.HasOrders)
            {
                editButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"✏️ {addon.Name}", CallbackData.Build("admin_edit_addon", addon.Id))
                });
            }
            else
            {
                editButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"✏️ {addon.Name}", CallbackData.Build("admin_edit_addon", addon.Id)),
                    InlineKeyboardButton.WithCallbackData($"❌ {addon.Name} (удалить)", CallbackData.Build("admin_del_addon", addon.Id))
                });
            }
        }

        editButtons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Закрыть", "admin_cancel") });

        return new ProjectEditForm(text, new InlineKeyboardMarkup(editButtons));
    }
}
