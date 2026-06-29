using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Models;
using FanFundingBot.Core.Constants;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;

namespace FanFundingBot.Bot.Services;

public class AdminService : IAdminService
{
    private readonly BotConfiguration _botConfig;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IQuestionRepository _questionRepository;
    private readonly IAddonRepository _addonRepository;
    private readonly IProjectStatusRepository _statusRepository;
    private readonly INotificationService _notificationService;

    public AdminService(
        BotConfiguration botConfig,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IQuestionRepository questionRepository,
        IAddonRepository addonRepository,
        IProjectStatusRepository statusRepository,
        INotificationService notificationService)
    {
        _botConfig = botConfig;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _questionRepository = questionRepository;
        _addonRepository = addonRepository;
        _statusRepository = statusRepository;
        _notificationService = notificationService;
    }

    public async Task<bool> IsAdminAsync(long userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        return userId == _botConfig.OwnerId || user != null && user.Role == UserRole.Admin;

    }

    public async Task EnsureAdminRoleAsync(long userId)
    {
        if (userId != _botConfig.OwnerId)
            return;

        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null && user.Role != UserRole.Admin)
        {
            user.Role = UserRole.Admin;
            await _userRepository.UpdateAsync(user);
        }
    }

    public async Task<string> UpdateProjectStatusAsync(Guid projectId, int statusId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return "Проект не найден.";

        var statusEntry = await _statusRepository.GetByIdAsync(statusId);
        if (statusEntry == null)
            return "Статус не найден.";

        project.StatusId = statusId;
        await _projectRepository.UpdateAsync(project);

        await _notificationService.NotifyProjectStatusUpdateAsync(
            projectId,
            project.Name,
            statusEntry.DisplayName);

        return $"✅ Статус проекта «{project.Name}» обновлён на «{statusEntry.DisplayName}».\n" +
               $"Уведомления отправлены подписанным пользователям.";
    }

    public async Task<string> BroadcastMessageAsync(Guid projectId, string message)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return "Проект не найден.";

        await _notificationService.BroadcastToProjectAsync(projectId, message);
        return "✅ Информационное сообщение отправлено всем подписанным пользователям.";
    }

    public async Task<List<Question>> GetUnansweredQuestionsAsync()
    {
        return await _questionRepository.GetUnansweredAsync();
    }

    public async Task<string> AnswerQuestionAsync(Guid questionId, string answer, long adminUserId)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        if (question == null)
            return "Вопрос не найден.";

        question.Answer = answer;
        question.AnsweredAt = DateTime.UtcNow;
        question.AnsweredByUserId = adminUserId;
        await _questionRepository.UpdateAsync(question);

        await _notificationService.NotifyQuestionAnsweredAsync(question.UserId, question.Text, answer);
        return "✅ Ответ отправлен пользователю.";
    }

    public async Task<Project?> GetProjectAsync(Guid projectId)
    {
        return await _projectRepository.GetByIdAsync(projectId);
    }

    public async Task<string> CreateProjectAsync(string name, string description, List<(string Name, string Description, decimal Price)>? addons)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            StatusId = KnownProjectStatusIds.Planned,
            CreatedAt = DateTime.UtcNow
        };

        if (addons != null)
        {
            foreach (var (addonName, addonDesc, price) in addons)
            {
                project.Addons.Add(new Addon
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = addonName,
                    Description = addonDesc,
                    Price = price
                });
            }
        }

        await _projectRepository.AddAsync(project);
        return $"✅ Проект «{name}» создан.";
    }

    public async Task<string> DeleteAddonAsync(Guid addonId)
    {
        var addon = await _addonRepository.GetByIdAsync(addonId);
        if (addon == null)
            return "Дополнение не найдено.";

        if (await _addonRepository.HasOrdersAsync(addonId))
            return "⚠️ Нельзя удалить дополнение, так как оно есть в заказах.";

        await _addonRepository.DeleteAsync(addonId);
        return "✅ Дополнение удалено.";
    }

    public async Task<string> UpdateProjectNameAsync(Guid projectId, string name)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return "Проект не найден.";

        project.Name = name;
        await _projectRepository.UpdateAsync(project);
        return $"✅ Название проекта изменено на «{name}».";
    }

    public async Task<string> UpdateProjectDescriptionAsync(Guid projectId, string description)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return "Проект не найден.";

        project.Description = description;
        await _projectRepository.UpdateAsync(project);
        return "✅ Описание проекта обновлено.";
    }

    public async Task<string> UpdateAddonNameAsync(Guid addonId, string name)
    {
        var addon = await _addonRepository.GetByIdAsync(addonId);
        if (addon == null)
            return "Дополнение не найдено.";

        addon.Name = name;
        await _addonRepository.UpdateAsync(addon);
        return $"✅ Название дополнения изменено на «{name}».";
    }

    public async Task<string> UpdateAddonDescriptionAsync(Guid addonId, string description)
    {
        var addon = await _addonRepository.GetByIdAsync(addonId);
        if (addon == null)
            return "Дополнение не найдено.";

        addon.Description = description;
        await _addonRepository.UpdateAsync(addon);
        return "✅ Описание дополнения обновлено.";
    }

    public async Task<string> UpdateAddonPriceAsync(Guid addonId, decimal price)
    {
        var addon = await _addonRepository.GetByIdAsync(addonId);
        if (addon == null)
            return "Дополнение не найдено.";

        addon.Price = price;
        await _addonRepository.UpdateAsync(addon);
        return $"✅ Цена дополнения изменена на {price:F2}₽.";
    }

    public async Task<string> CreateAddonAsync(Guid projectId, string name, string description, decimal price)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return "Проект не найден.";

        var addon = new Addon
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Description = description,
            Price = price
        };

        await _addonRepository.AddAsync(addon);
        return $"✅ Дополнение «{name}» добавлено в проект «{project.Name}».";
    }

    public async Task<string> CreateProjectStatusAsync(string displayName, int order, bool canEdit)
    {
        var entry = new ProjectStatusEntry
        {
            DisplayName = displayName,
            Order = order,
            CanEdit = canEdit
        };

        await _statusRepository.AddAsync(entry);
        return $"✅ Статус «{displayName}» добавлен.";
    }

    public async Task<List<ProjectStatusEntry>> GetAllStatusesAsync()
    {
        return await _statusRepository.GetAllAsync();
    }
}
