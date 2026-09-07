using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Tests;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class AdminStatusCallbackHandlerTests
{
    private IAdminService _adminService;
    private AdminStatusCallbackHandler _handler;
    private ITelegramBotClient _botClient;

    private static readonly Guid ProjectId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _adminService = Substitute.For<IAdminService>();
        var menuHelper = new MainMenuHelper(_adminService);
        _handler = new AdminStatusCallbackHandler(_adminService, menuHelper);
        _botClient = Substitute.For<ITelegramBotClient>();
        _botClient.TestApi(Arg.Any<CancellationToken>()).Returns(true);
    }

    private static CallbackQuery MakeCq(string data)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 100);
        SetProp(msg, "Chat", new Chat { Id = 200 });

        var cq = new CallbackQuery();
        SetProp(cq, "Id", "cq_id");
        SetProp(cq, "Data", data);
        SetProp(cq, "Message", msg);
        SetProp(cq, "From", new Telegram.Bot.Types.User { Id = 1, FirstName = "Admin" });
        return cq;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    [Test]
    public async Task AdminStatus_ShowsConfirmationDialog()
    {
        const int statusId = 5;
        var currentStatus = new ProjectStatus("Current", 1, true);
        var newStatus = new ProjectStatus("New Status", 2, true);

        var project = DomainTestFactory.NewProject("Test Project");
        _adminService.GetProjectAsync(ProjectId).Returns(project);
        _adminService.GetAllStatusesAsync()
            .Returns(new List<ProjectStatus> { currentStatus, newStatus });

        await _handler.HandleAdminStatusAsync(_botClient, MakeCq($"admin_status:{ProjectId}:{statusId}"), 1,
            $"admin_status:{ProjectId}:{statusId}", CancellationToken.None);

        await _adminService.DidNotReceive().UpdateProjectStatusAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }

    [Test]
    public async Task AdminStatus_NullProject_ShowsNotFound()
    {
        _adminService.GetProjectAsync(ProjectId).Returns((Project?)null);

        await _handler.HandleAdminStatusAsync(_botClient, MakeCq($"admin_status:{ProjectId}:5"), 1,
            $"admin_status:{ProjectId}:5", CancellationToken.None);

        await _adminService.DidNotReceive().UpdateProjectStatusAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }

    [Test]
    public async Task AdminStatusConfirm_UpdatesStatusAndEditsMessage()
    {
        const int statusId = 5;
        _adminService.UpdateProjectStatusAsync(ProjectId, statusId)
            .Returns("✅ Статус обновлён.");

        await _handler.HandleAdminStatusConfirmAsync(_botClient, MakeCq($"admin_status_confirm:{ProjectId}:{statusId}"), 1,
            $"admin_status_confirm:{ProjectId}:{statusId}", CancellationToken.None);

        await _adminService.Received(1).UpdateProjectStatusAsync(ProjectId, statusId);
    }

    [Test]
    public async Task AdminStatusCancel_ReturnsToStatusSelection()
    {
        _adminService.GetProjectAsync(ProjectId)
            .Returns(DomainTestFactory.NewProject("Test Project"));
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());

        await _handler.HandleAdminStatusCancelAsync(_botClient, MakeCq($"admin_status_cancel:{ProjectId}"), 1,
            $"admin_status_cancel:{ProjectId}", CancellationToken.None);
    }

    [Test]
    public async Task AdminStatusCancel_NullProject_ShowsNotFound()
    {
        _adminService.GetProjectAsync(ProjectId).Returns((Project?)null);

        await _handler.HandleAdminStatusCancelAsync(_botClient, MakeCq($"admin_status_cancel:{ProjectId}"), 1,
            $"admin_status_cancel:{ProjectId}", CancellationToken.None);
    }
}

[TestFixture]
public class AdminQuestionCallbackHandlerTests
{
    private IUserStateService _stateService;
    private IAdminService _adminService;
    private IProjectRepository _projectRepo;
    private AdminQuestionCallbackHandler _handler;
    private ITelegramBotClient _botClient;
    private UserSession _session;

    private static readonly Guid ProjectId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _adminService = Substitute.For<IAdminService>();
        _stateService = Substitute.For<IUserStateService>();
        _projectRepo = Substitute.For<IProjectRepository>();
        var menuHelper = new MainMenuHelper(_adminService);
        _handler = new AdminQuestionCallbackHandler(
            _stateService, _projectRepo, _adminService, menuHelper);
        _botClient = Substitute.For<ITelegramBotClient>();
        _botClient.TestApi(Arg.Any<CancellationToken>()).Returns(true);
        _session = new UserSession();
    }

    private static CallbackQuery MakeCq(string data)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 100);
        SetProp(msg, "Chat", new Chat { Id = 200 });

        var cq = new CallbackQuery();
        SetProp(cq, "Id", "cq_id");
        SetProp(cq, "Data", data);
        SetProp(cq, "Message", msg);
        SetProp(cq, "From", new Telegram.Bot.Types.User { Id = 1, FirstName = "Admin" });
        return cq;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    [Test]
    public async Task BroadcastConfirm_SendsBroadcastAndClearsSession()
    {
        var messageText = "Important announcement!";
        _session.TempBroadcastText = messageText;
        _stateService.Get(1).Returns(_session);
        _projectRepo.GetAllAsync().Returns(new List<Project>
        {
            DomainTestFactory.NewProjectWithId(ProjectId, "Test")
        });
        _adminService.BroadcastMessageAsync(ProjectId, messageText)
            .Returns("✅ Рассылка выполнена.");

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _adminService.Received(1).BroadcastMessageAsync(ProjectId, messageText);
        Assert.That(_session.PendingAction, Is.Null);
        Assert.That(_session.TempBroadcastText, Is.Null);
    }

    [Test]
    public async Task BroadcastConfirm_NullText_ReturnsEarly()
    {
        _stateService.Get(1).Returns(_session);

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _adminService.DidNotReceive().BroadcastMessageAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Test]
    public async Task BroadcastConfirm_NoProjects_ShowsError()
    {
        _session.TempBroadcastText = "text";
        _stateService.Get(1).Returns(_session);
        _projectRepo.GetAllAsync().Returns(new List<Project>());

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _adminService.DidNotReceive().BroadcastMessageAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Test]
    public async Task BroadcastConfirm_NullSession_ReturnsEarly()
    {
        _stateService.Get(1).Returns((UserSession?)null);

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _adminService.DidNotReceive().BroadcastMessageAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Test]
    public async Task AdminSelectQuestion_ShowsUsername_WhenPresent()
    {
        var qId = Guid.NewGuid();
        var user = DomainTestFactory.NewCustomer(5, "testuser");
        _adminService.GetUnansweredQuestionsAsync().Returns(new List<Question>
        {
            new Question(user, "Q?")
        });
        _stateService.GetOrCreate(1).Returns(_session);

        await _handler.HandleAdminSelectQuestionAsync(_botClient, MakeCq("admin_answer:" + qId), 1,
            "admin_answer:" + qId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_session.PendingAction, Is.EqualTo("answer_question"));
            Assert.That(_session.PendingQuestionId, Is.EqualTo(qId));
        });
    }

    [Test]
    public async Task AdminSelectQuestion_HidesUsername_WhenNull()
    {
        var qId = Guid.NewGuid();
        var user = DomainTestFactory.NewCustomer(5, null);
        _adminService.GetUnansweredQuestionsAsync().Returns(new List<Question>
        {
            new Question(user, "Q?")
        });
        _stateService.GetOrCreate(1).Returns(_session);

        await _handler.HandleAdminSelectQuestionAsync(_botClient, MakeCq("admin_answer:" + qId), 1,
            "admin_answer:" + qId, CancellationToken.None);

        Assert.That(_session.PendingAction, Is.EqualTo("answer_question"));
    }

    [Test]
    public async Task AdminCancel_ShowsCancellationMessage()
    {
        await _handler.HandleAdminCancelAsync(_botClient, MakeCq("admin_cancel"), 1, CancellationToken.None);
    }
}

[TestFixture]
public class AdminProjectCallbackHandlerTests
{
    private IUserStateService _stateService;
    private IAdminService _adminService;
    private IAddonRepository _addonRepo;
    private AdminProjectCallbackHandler _handler;
    private ITelegramBotClient _botClient;
    private UserSession _session;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AddonId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _adminService = Substitute.For<IAdminService>();
        _stateService = Substitute.For<IUserStateService>();
        _addonRepo = Substitute.For<IAddonRepository>();
        var menuHelper = new MainMenuHelper(_adminService);
        var editFormBuilder = new ProjectEditFormBuilder(_adminService, _addonRepo);
        _handler = new AdminProjectCallbackHandler(
            _stateService, _adminService, _addonRepo, editFormBuilder, menuHelper);
        _botClient = Substitute.For<ITelegramBotClient>();
        _botClient.TestApi(Arg.Any<CancellationToken>()).Returns(true);
        _session = new UserSession();
    }

    private static CallbackQuery MakeCq(string data)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 100);
        SetProp(msg, "Chat", new Chat { Id = 200 });

        var cq = new CallbackQuery();
        SetProp(cq, "Id", "cq_id");
        SetProp(cq, "Data", data);
        SetProp(cq, "Message", msg);
        SetProp(cq, "From", new Telegram.Bot.Types.User { Id = 1, FirstName = "Admin" });
        return cq;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    [Test]
    public async Task CreateProjectConfirm_CreatesAndRemovesSession()
    {
        _session.TempProjectName = "P";
        _session.TempProjectDescription = "D";
        _stateService.Get(1).Returns(_session);
        _adminService.CreateProjectAsync(1, "P", "D", null).Returns("✅");
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        _stateService.Received(1).Remove(1);
    }

    [Test]
    public async Task CreateProjectConfirm_NullName_ReturnsEarly()
    {
        _stateService.Get(1).Returns(_session);
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        await _adminService.DidNotReceiveWithAnyArgs().CreateProjectAsync(default, default, default, default);
    }

    [Test]
    public async Task CreateProjectConfirm_WithAddons_PassesThem()
    {
        _session.TempProjectName = "P";
        _session.TempAddons = new List<(string, string, decimal)> { ("A", "D", 10m) };
        _stateService.Get(1).Returns(_session);
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        _stateService.Received(1).Remove(1);
    }

    [Test]
    public async Task EditProject_ShowsEditForm()
    {
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _adminService.GetProjectAsync(ProjectId).Returns(DomainTestFactory.NewProject("Test"));
        _addonRepo.GetByProjectIdAsync(ProjectId).Returns(new List<Addon>());
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_NullProject_ShowsNotFound()
    {
        _adminService.GetProjectAsync(ProjectId).Returns((Project?)null);
        await _handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_WithAddons_IncludesSummary()
    {
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _adminService.GetProjectAsync(ProjectId).Returns(DomainTestFactory.NewProject("P"));
        _addonRepo.GetByProjectIdAsync(ProjectId)
            .Returns(new List<Addon> { DomainTestFactory.NewAddon(AddonId, "A1", 100m) });
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_ResetsPendingAction()
    {
        _session.PendingAction = "old";
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _adminService.GetProjectAsync(ProjectId).Returns(DomainTestFactory.NewProject("P"));
        _addonRepo.GetByProjectIdAsync(ProjectId).Returns(new List<Addon>());
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
        Assert.That(_session.PendingAction, Is.Null);
    }

    [Test]
    public async Task EditProjectName_SetsPendingAction()
    {
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectNameAsync(_botClient, MakeCq("admin_edit_project_name:" + ProjectId), 1,
            "admin_edit_project_name:" + ProjectId, CancellationToken.None);
        Assert.That(_session.PendingAction, Is.EqualTo($"edit_project_name:{ProjectId:N}"));
    }

    [Test]
    public async Task EditProjectDesc_SetsPendingAction()
    {
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectDescAsync(_botClient, MakeCq("admin_edit_project_desc:" + ProjectId), 1,
            "admin_edit_project_desc:" + ProjectId, CancellationToken.None);
        Assert.That(_session.PendingAction, Is.EqualTo($"edit_project_desc:{ProjectId:N}"));
    }

    [Test]
    public async Task AddAddon_SetsPendingActionAndTempId()
    {
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminAddAddonAsync(_botClient, MakeCq("admin_edit_add_addon:" + ProjectId), 1,
            "admin_edit_add_addon:" + ProjectId, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(_session.TempProjectId, Is.EqualTo(ProjectId));
            Assert.That(_session.PendingAction, Is.EqualTo($"add_addon_name:{ProjectId:N}"));
        });
    }

    [Test]
    public async Task EditAddon_SetsPendingActionAndTempId()
    {
        var addonId = Guid.NewGuid();
        _stateService.GetOrCreate(1).Returns(_session);
        _addonRepo.GetByIdAsync(addonId).Returns(DomainTestFactory.NewAddon(addonId, "Test Addon"));

        await _handler.HandleAdminEditAddonAsync(_botClient, MakeCq($"admin_edit_addon:{addonId}"), 1,
            $"admin_edit_addon:{addonId}", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_session.TempAddonId, Is.EqualTo(addonId));
            Assert.That(_session.PendingAction, Is.EqualTo($"wizard_addon_name:{addonId:N}"));
        });
    }

    [Test]
    public async Task DeleteAddon_ShowsConfirmation()
    {
        var addonId = Guid.NewGuid();
        _addonRepo.GetByIdAsync(addonId).Returns(DomainTestFactory.NewAddon(addonId, "Test"));
        _addonRepo.GetProjectIdByAddonAsync(addonId).Returns(ProjectId);

        await _handler.HandleAdminDeleteAddonAsync(_botClient, MakeCq($"admin_del_addon:{addonId}"), 1,
            $"admin_del_addon:{addonId}", CancellationToken.None);
    }

    [Test]
    public async Task DeleteAddonConfirm_DeletesAndShowsResult()
    {
        var addonId = Guid.NewGuid();
        _addonRepo.GetByIdAsync(addonId).Returns(DomainTestFactory.NewAddon(addonId, "Test"));
        _addonRepo.GetProjectIdByAddonAsync(addonId).Returns(ProjectId);
        _adminService.DeleteAddonAsync(addonId).Returns("✅ Дополнение удалено.");
        _adminService.GetProjectAsync(ProjectId).Returns(DomainTestFactory.NewProject("P"));
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _addonRepo.GetByProjectIdAsync(Arg.Any<Guid>()).Returns(new List<Addon>());
        _stateService.GetOrCreate(1).Returns(_session);

        await _handler.HandleAdminDeleteAddonConfirmAsync(_botClient, MakeCq($"admin_del_addon_confirm:{addonId}"), 1,
            $"admin_del_addon_confirm:{addonId}", CancellationToken.None);

        await _adminService.Received(1).DeleteAddonAsync(addonId);
    }

    [Test]
    public async Task DeleteAddonCancel_ReturnsToEditForm()
    {
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _adminService.GetProjectAsync(ProjectId).Returns(DomainTestFactory.NewProject("P"));
        _addonRepo.GetByProjectIdAsync(ProjectId).Returns(new List<Addon>());
        _stateService.GetOrCreate(1).Returns(_session);

        await _handler.HandleAdminDeleteAddonCancelAsync(_botClient, MakeCq($"admin_del_addon_cancel:{ProjectId}"), 1,
            $"admin_del_addon_cancel:{ProjectId}", CancellationToken.None);
    }

    [Test]
    public async Task AddStatus_SetsPendingAction()
    {
        _stateService.GetOrCreate(1).Returns(_session);

        await _handler.HandleAdminAddStatusAsync(_botClient, MakeCq($"admin_add_status:{ProjectId}"), 1,
            $"admin_add_status:{ProjectId}", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_session.PendingAction, Is.EqualTo("add_status_name"));
            Assert.That(_session.TempProjectId, Is.EqualTo(ProjectId));
        });
    }

    [Test]
    public async Task AddStatusCanEdit_Yes_CreatesStatusWithCanEditTrue()
    {
        _session.TempStatusName = "Test Status";
        _stateService.Get(1).Returns(_session);
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _adminService.CreateProjectStatusAsync("Test Status", 1, true)
            .Returns("✅ Статус добавлен.");

        await _handler.HandleAdminAddStatusCanEditAsync(_botClient, MakeCq("admin_add_status_can_edit:yes"), 1,
            "admin_add_status_can_edit:yes", CancellationToken.None);

        await _adminService.Received(1).CreateProjectStatusAsync("Test Status", 1, true);
    }

    [Test]
    public async Task AddStatusCanEdit_No_CreatesStatusWithCanEditFalse()
    {
        _session.TempStatusName = "Test Status";
        _stateService.Get(1).Returns(_session);
        _adminService.GetAllStatusesAsync().Returns(new List<ProjectStatus>());
        _adminService.CreateProjectStatusAsync("Test Status", 1, false)
            .Returns("✅ Статус добавлен.");

        await _handler.HandleAdminAddStatusCanEditAsync(_botClient, MakeCq("admin_add_status_can_edit:no"), 1,
            "admin_add_status_can_edit:no", CancellationToken.None);

        await _adminService.Received(1).CreateProjectStatusAsync("Test Status", 1, false);
    }

    [Test]
    public async Task AddStatusCanEdit_NullName_ReturnsEarly()
    {
        _stateService.Get(1).Returns(_session);
        await _handler.HandleAdminAddStatusCanEditAsync(_botClient, MakeCq("admin_add_status_can_edit:yes"), 1,
            "admin_add_status_can_edit:yes", CancellationToken.None);
        await _adminService.DidNotReceiveWithAnyArgs().CreateProjectStatusAsync(default, default, default);
    }
}
