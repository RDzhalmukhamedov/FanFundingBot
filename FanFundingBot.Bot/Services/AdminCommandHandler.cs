using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class AdminCommandHandler : IAdminCommandHandler
{
    private readonly IUserStateService _stateService;
    private readonly IAdminService _adminService;
    private readonly IProjectRepository _projectRepository;
    private readonly IQuestionRepository _questionRepository;
    private readonly MainMenuHelper _menuHelper;

    public AdminCommandHandler(
        IUserStateService stateService,
        IAdminService adminService,
        IProjectRepository projectRepository,
        IQuestionRepository questionRepository,
        MainMenuHelper menuHelper)
    {
        _stateService = stateService;
        _adminService = adminService;
        _projectRepository = projectRepository;
        _questionRepository = questionRepository;
        _menuHelper = menuHelper;
    }

    public async Task HandleAdminUpdateStatusAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projects = await _projectRepository.GetAllAsync();
        if (projects.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Нет проектов для изменения статуса.",
                replyMarkup: await _menuHelper.CreateKeyboardAsync(userId),
                cancellationToken: ct);
            return;
        }

        var project = projects.First();
        var statuses = await _adminService.GetAllStatusesAsync();
        var (keyboard, _) = StatusSelectionKeyboard.Create(statuses, project.Id, 1);

        await botClient.SendMessage(
            message.Chat.Id,
            $"Выберите новый статус для проекта «{project.Name}»:",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleAdminBroadcastAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "broadcast";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "📢 Введите текст сообщения для рассылки:",
            replyMarkup: BackKeyboard.Create(),
            cancellationToken: ct);
    }

    public async Task HandleAdminAnswerQuestionAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var questions = await _adminService.GetUnansweredQuestionsAsync();

        if (questions.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Нет неотвеченных вопросов.",
                replyMarkup: await _menuHelper.CreateKeyboardAsync(userId),
                cancellationToken: ct);
            return;
        }

        var keyboard = new List<InlineKeyboardButton[]>();
        foreach (var q in questions)
        {
            var username = q.User.Username != null ? $"@{q.User.Username}" : $"ID {q.User.Id}";
            keyboard.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"✉️ {q.Text[..Math.Min(q.Text.Length, 30)]} — {username}",
                    CallbackData.Build("admin_answer", q.Id))
            });
        }

        await botClient.SendMessage(
            message.Chat.Id,
            "Выберите вопрос для ответа:",
            replyMarkup: new InlineKeyboardMarkup(keyboard),
            cancellationToken: ct);
    }

    public async Task HandleAdminCreateProjectAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "create_project_name";
        session.TempProjectName = null;
        session.TempProjectDescription = null;
        session.TempAddons = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Введите название нового проекта:",
            replyMarkup: BackKeyboard.Create(),
            cancellationToken: ct);
    }

    public async Task HandleAdminEditProjectListAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projects = await _projectRepository.GetAllAsync();

        if (projects.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Нет проектов для редактирования.",
                replyMarkup: await _menuHelper.CreateKeyboardAsync(userId),
                cancellationToken: ct);
            return;
        }

        var keyboard = new List<InlineKeyboardButton[]>();
        foreach (var project in projects)
        {
            keyboard.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"✏️ {project.Name}",
                    CallbackData.Build("admin_edit_project", project.Id))
            });
        }

        await botClient.SendMessage(
            message.Chat.Id,
            "Выберите проект для редактирования:",
            replyMarkup: new InlineKeyboardMarkup(keyboard),
            cancellationToken: ct);
    }
}