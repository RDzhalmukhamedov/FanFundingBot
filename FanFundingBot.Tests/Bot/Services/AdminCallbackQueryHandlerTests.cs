using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class AdminStatusCallbackHandlerTests
{
    private Mock<IAdminService> _adminServiceMock;
    private AdminStatusCallbackHandler _handler;
    private Mock<ITelegramBotClient> _botClientMock;

    private static readonly Guid ProjectId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _adminServiceMock = new Mock<IAdminService>();
        var menuHelper = new MainMenuHelper(_adminServiceMock.Object);
        _handler = new AdminStatusCallbackHandler(_adminServiceMock.Object, menuHelper);
        _botClientMock = new Mock<ITelegramBotClient>();
        _botClientMock
            .Setup(c => c.TestApi(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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
        var projectName = "Test Project";
        var currentStatus = new ProjectStatus { Id = 3, DisplayName = "Current" };
        var newStatus = new ProjectStatus { Id = statusId, DisplayName = "New Status" };

        _adminServiceMock
            .Setup(s => s.GetProjectAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Name = projectName, StatusId = currentStatus.Id });
        _adminServiceMock
            .Setup(s => s.GetAllStatusesAsync())
            .ReturnsAsync(new List<ProjectStatus> { currentStatus, newStatus });

        await _handler.HandleAdminStatusAsync(_botClientMock.Object, MakeCq($"admin_status:{ProjectId}:{statusId}"), 1,
            $"admin_status:{ProjectId}:{statusId}", CancellationToken.None);

        _adminServiceMock.Verify(s => s.UpdateProjectStatusAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task AdminStatus_NullProject_ShowsNotFound()
    {
        _adminServiceMock
            .Setup(s => s.GetProjectAsync(ProjectId))
            .ReturnsAsync((Project?)null);

        await _handler.HandleAdminStatusAsync(_botClientMock.Object, MakeCq($"admin_status:{ProjectId}:5"), 1,
            $"admin_status:{ProjectId}:5", CancellationToken.None);

        _adminServiceMock.Verify(s => s.UpdateProjectStatusAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task AdminStatusConfirm_UpdatesStatusAndEditsMessage()
    {
        const int statusId = 5;
        _adminServiceMock
            .Setup(s => s.UpdateProjectStatusAsync(ProjectId, statusId))
            .ReturnsAsync("✅ Статус обновлён.");

        await _handler.HandleAdminStatusConfirmAsync(_botClientMock.Object, MakeCq($"admin_status_confirm:{ProjectId}:{statusId}"), 1,
            $"admin_status_confirm:{ProjectId}:{statusId}", CancellationToken.None);

        _adminServiceMock.Verify(s => s.UpdateProjectStatusAsync(ProjectId, statusId), Times.Once);
    }

    [Test]
    public async Task AdminStatusCancel_ReturnsToStatusSelection()
    {
        var projectName = "Test Project";
        _adminServiceMock
            .Setup(s => s.GetProjectAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Name = projectName, StatusId = 1 });
        _adminServiceMock
            .Setup(s => s.GetAllStatusesAsync())
            .ReturnsAsync(new List<ProjectStatus>());

        await _handler.HandleAdminStatusCancelAsync(_botClientMock.Object, MakeCq($"admin_status_cancel:{ProjectId}"), 1,
            $"admin_status_cancel:{ProjectId}", CancellationToken.None);
    }

    [Test]
    public async Task AdminStatusCancel_NullProject_ShowsNotFound()
    {
        _adminServiceMock
            .Setup(s => s.GetProjectAsync(ProjectId))
            .ReturnsAsync((Project?)null);

        await _handler.HandleAdminStatusCancelAsync(_botClientMock.Object, MakeCq($"admin_status_cancel:{ProjectId}"), 1,
            $"admin_status_cancel:{ProjectId}", CancellationToken.None);
    }
}

[TestFixture]
public class AdminQuestionCallbackHandlerTests
{
    private Mock<IUserStateService> _stateServiceMock;
    private Mock<IAdminService> _adminServiceMock;
    private Mock<IProjectRepository> _projectRepoMock;
    private AdminQuestionCallbackHandler _handler;
    private Mock<ITelegramBotClient> _botClientMock;
    private UserSession _session;

    private static readonly Guid ProjectId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _adminServiceMock = new Mock<IAdminService>();
        _stateServiceMock = new Mock<IUserStateService>();
        _projectRepoMock = new Mock<IProjectRepository>();
        var menuHelper = new MainMenuHelper(_adminServiceMock.Object);
        _handler = new AdminQuestionCallbackHandler(
            _stateServiceMock.Object, _projectRepoMock.Object, _adminServiceMock.Object, menuHelper);
        _botClientMock = new Mock<ITelegramBotClient>();
        _botClientMock
            .Setup(c => c.TestApi(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        _projectRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>
        {
            new() { Id = ProjectId, Name = "Test" }
        });
        _adminServiceMock
            .Setup(s => s.BroadcastMessageAsync(ProjectId, messageText))
            .ReturnsAsync("✅ Рассылка выполнена.");

        await _handler.HandleAdminBroadcastConfirmAsync(_botClientMock.Object, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        _adminServiceMock.Verify(s => s.BroadcastMessageAsync(ProjectId, messageText), Times.Once);
        Assert.That(_session.PendingAction, Is.Null);
        Assert.That(_session.TempBroadcastText, Is.Null);
    }

    [Test]
    public async Task BroadcastConfirm_NullText_ReturnsEarly()
    {
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);

        await _handler.HandleAdminBroadcastConfirmAsync(_botClientMock.Object, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        _adminServiceMock.Verify(s => s.BroadcastMessageAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task BroadcastConfirm_NoProjects_ShowsError()
    {
        _session.TempBroadcastText = "text";
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        _projectRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());

        await _handler.HandleAdminBroadcastConfirmAsync(_botClientMock.Object, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        _adminServiceMock.Verify(s => s.BroadcastMessageAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task BroadcastConfirm_NullSession_ReturnsEarly()
    {
        _stateServiceMock.Setup(s => s.Get(1)).Returns((UserSession?)null);

        await _handler.HandleAdminBroadcastConfirmAsync(_botClientMock.Object, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        _adminServiceMock.Verify(s => s.BroadcastMessageAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AdminSelectQuestion_ShowsUsername_WhenPresent()
    {
        var qId = Guid.NewGuid();
        var user = new FanFundingBot.Core.Entities.Customer { Id = 5, FirstName = "User1", Username = "testuser" };
        _adminServiceMock.Setup(s => s.GetUnansweredQuestionsAsync()).ReturnsAsync(new List<Question>
        {
            new() { Id = qId, UserId = 5, Text = "Q?", User = user }
        });
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);

        await _handler.HandleAdminSelectQuestionAsync(_botClientMock.Object, MakeCq("admin_answer:" + qId), 1,
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
        var user = new FanFundingBot.Core.Entities.Customer { Id = 5, FirstName = "User1", Username = null };
        _adminServiceMock.Setup(s => s.GetUnansweredQuestionsAsync()).ReturnsAsync(new List<Question>
        {
            new() { Id = qId, UserId = 5, Text = "Q?", User = user }
        });
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);

        await _handler.HandleAdminSelectQuestionAsync(_botClientMock.Object, MakeCq("admin_answer:" + qId), 1,
            "admin_answer:" + qId, CancellationToken.None);

        Assert.That(_session.PendingAction, Is.EqualTo("answer_question"));
    }

    [Test]
    public async Task AdminCancel_ShowsCancellationMessage()
    {
        await _handler.HandleAdminCancelAsync(_botClientMock.Object, MakeCq("admin_cancel"), 1, CancellationToken.None);
    }
}

[TestFixture]
public class AdminProjectCallbackHandlerTests
{
    private Mock<IUserStateService> _stateServiceMock;
    private Mock<IAdminService> _adminServiceMock;
    private Mock<IAddonRepository> _addonRepoMock;
    private AdminProjectCallbackHandler _handler;
    private Mock<ITelegramBotClient> _botClientMock;
    private UserSession _session;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AddonId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _adminServiceMock = new Mock<IAdminService>();
        _stateServiceMock = new Mock<IUserStateService>();
        _addonRepoMock = new Mock<IAddonRepository>();
        var menuHelper = new MainMenuHelper(_adminServiceMock.Object);
        var editFormBuilder = new ProjectEditFormBuilder(_adminServiceMock.Object, _addonRepoMock.Object);
        _handler = new AdminProjectCallbackHandler(
            _stateServiceMock.Object, _adminServiceMock.Object, _addonRepoMock.Object, editFormBuilder, menuHelper);
        _botClientMock = new Mock<ITelegramBotClient>();
        _botClientMock
            .Setup(c => c.TestApi(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        _adminServiceMock.Setup(s => s.CreateProjectAsync("P", "D", null)).ReturnsAsync("✅");
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClientMock.Object, MakeCq("x"), 1, CancellationToken.None);
        _stateServiceMock.Verify(s => s.Remove(1));
    }

    [Test]
    public async Task CreateProjectConfirm_NullName_ReturnsEarly()
    {
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClientMock.Object, MakeCq("x"), 1, CancellationToken.None);
        _adminServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateProjectConfirm_WithAddons_PassesThem()
    {
        _session.TempProjectName = "P";
        _session.TempAddons = new List<(string, string, decimal)> { ("A", "D", 10m) };
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClientMock.Object, MakeCq("x"), 1, CancellationToken.None);
        _stateServiceMock.Verify(s => s.Remove(1));
    }

    [Test]
    public async Task EditProject_ShowsEditForm()
    {
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _adminServiceMock.Setup(s => s.GetProjectAsync(ProjectId)).ReturnsAsync(new Project { Id = ProjectId, Name = "Test" });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(ProjectId)).ReturnsAsync(new List<Addon>());
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClientMock.Object, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_NullProject_ShowsNotFound()
    {
        _adminServiceMock.Setup(s => s.GetProjectAsync(ProjectId)).ReturnsAsync((Project?)null);
        await _handler.HandleAdminEditProjectAsync(_botClientMock.Object, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_WithAddons_IncludesSummary()
    {
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _adminServiceMock.Setup(s => s.GetProjectAsync(ProjectId)).ReturnsAsync(new Project { Id = ProjectId, Name = "P" });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(ProjectId)).ReturnsAsync(new List<Addon> { new() { Id = AddonId, Name = "A1", Price = 100m } });
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClientMock.Object, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_ResetsPendingAction()
    {
        _session.PendingAction = "old";
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _adminServiceMock.Setup(s => s.GetProjectAsync(ProjectId)).ReturnsAsync(new Project { Id = ProjectId, Name = "P" });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(ProjectId)).ReturnsAsync(new List<Addon>());
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClientMock.Object, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
        Assert.That(_session.PendingAction, Is.Null);
    }

    [Test]
    public async Task EditProjectName_SetsPendingAction()
    {
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        await _handler.HandleAdminEditProjectNameAsync(_botClientMock.Object, MakeCq("admin_edit_project_name:" + ProjectId), 1,
            "admin_edit_project_name:" + ProjectId, CancellationToken.None);
        Assert.That(_session.PendingAction, Is.EqualTo($"edit_project_name:{ProjectId:N}"));
    }

    [Test]
    public async Task EditProjectDesc_SetsPendingAction()
    {
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        await _handler.HandleAdminEditProjectDescAsync(_botClientMock.Object, MakeCq("admin_edit_project_desc:" + ProjectId), 1,
            "admin_edit_project_desc:" + ProjectId, CancellationToken.None);
        Assert.That(_session.PendingAction, Is.EqualTo($"edit_project_desc:{ProjectId:N}"));
    }

    [Test]
    public async Task AddAddon_SetsPendingActionAndTempId()
    {
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        await _handler.HandleAdminAddAddonAsync(_botClientMock.Object, MakeCq("admin_edit_add_addon:" + ProjectId), 1,
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
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);
        _addonRepoMock.Setup(r => r.GetByIdAsync(addonId)).ReturnsAsync(new Addon { Id = addonId, Name = "Test Addon" });

        await _handler.HandleAdminEditAddonAsync(_botClientMock.Object, MakeCq($"admin_edit_addon:{addonId}"), 1,
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
        _addonRepoMock.Setup(r => r.GetByIdAsync(addonId)).ReturnsAsync(new Addon { Id = addonId, Name = "Test", ProjectId = ProjectId });

        await _handler.HandleAdminDeleteAddonAsync(_botClientMock.Object, MakeCq($"admin_del_addon:{addonId}"), 1,
            $"admin_del_addon:{addonId}", CancellationToken.None);
    }

    [Test]
    public async Task DeleteAddonConfirm_DeletesAndShowsResult()
    {
        var addonId = Guid.NewGuid();
        _addonRepoMock.Setup(r => r.GetByIdAsync(addonId)).ReturnsAsync(new Addon { Id = addonId, Name = "Test", ProjectId = ProjectId });
        _adminServiceMock.Setup(s => s.DeleteAddonAsync(addonId)).ReturnsAsync("✅ Дополнение удалено.");
        _adminServiceMock.Setup(s => s.GetProjectAsync(ProjectId)).ReturnsAsync(new Project { Id = ProjectId, Name = "P", StatusId = 1 });
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Addon>());
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);

        await _handler.HandleAdminDeleteAddonConfirmAsync(_botClientMock.Object, MakeCq($"admin_del_addon_confirm:{addonId}"), 1,
            $"admin_del_addon_confirm:{addonId}", CancellationToken.None);

        _adminServiceMock.Verify(s => s.DeleteAddonAsync(addonId), Times.Once);
    }

    [Test]
    public async Task DeleteAddonCancel_ReturnsToEditForm()
    {
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _adminServiceMock.Setup(s => s.GetProjectAsync(ProjectId)).ReturnsAsync(new Project { Id = ProjectId, Name = "P", StatusId = 1 });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(ProjectId)).ReturnsAsync(new List<Addon>());
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);

        await _handler.HandleAdminDeleteAddonCancelAsync(_botClientMock.Object, MakeCq($"admin_del_addon_cancel:{ProjectId}"), 1,
            $"admin_del_addon_cancel:{ProjectId}", CancellationToken.None);
    }

    [Test]
    public async Task AddStatus_SetsPendingAction()
    {
        _stateServiceMock.Setup(s => s.GetOrCreate(1)).Returns(_session);

        await _handler.HandleAdminAddStatusAsync(_botClientMock.Object, MakeCq($"admin_add_status:{ProjectId}"), 1,
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
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _adminServiceMock.Setup(s => s.CreateProjectStatusAsync("Test Status", 1, true))
            .ReturnsAsync("✅ Статус добавлен.");

        await _handler.HandleAdminAddStatusCanEditAsync(_botClientMock.Object, MakeCq("admin_add_status_can_edit:yes"), 1,
            "admin_add_status_can_edit:yes", CancellationToken.None);

        _adminServiceMock.Verify(s => s.CreateProjectStatusAsync("Test Status", 1, true), Times.Once);
    }

    [Test]
    public async Task AddStatusCanEdit_No_CreatesStatusWithCanEditFalse()
    {
        _session.TempStatusName = "Test Status";
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        _adminServiceMock.Setup(s => s.GetAllStatusesAsync()).ReturnsAsync(new List<ProjectStatus>());
        _adminServiceMock.Setup(s => s.CreateProjectStatusAsync("Test Status", 1, false))
            .ReturnsAsync("✅ Статус добавлен.");

        await _handler.HandleAdminAddStatusCanEditAsync(_botClientMock.Object, MakeCq("admin_add_status_can_edit:no"), 1,
            "admin_add_status_can_edit:no", CancellationToken.None);

        _adminServiceMock.Verify(s => s.CreateProjectStatusAsync("Test Status", 1, false), Times.Once);
    }

    [Test]
    public async Task AddStatusCanEdit_NullName_ReturnsEarly()
    {
        _stateServiceMock.Setup(s => s.Get(1)).Returns(_session);
        await _handler.HandleAdminAddStatusCanEditAsync(_botClientMock.Object, MakeCq("admin_add_status_can_edit:yes"), 1,
            "admin_add_status_can_edit:yes", CancellationToken.None);
        _adminServiceMock.VerifyNoOtherCalls();
    }
}