using System.Reflection;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Entities;
using FluentAssertions;
using FluentResults;
using MediatR;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class AdminStatusCallbackHandlerTests
{
    private IMediator _mediator;
    private AdminStatusCallbackHandler _handler;
    private ITelegramBotClient _botClient;

    private static readonly Guid ProjectId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _mediator = Substitute.For<IMediator>();
        var authorization = Substitute.For<IAuthorizationService>();
        var menuHelper = new MainMenuHelper(authorization);
        _handler = new AdminStatusCallbackHandler(_mediator, menuHelper);
        _botClient = Substitute.For<ITelegramBotClient>();
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

    private static ProjectDto TestProject(string name = "Test Project")
    {
        return new ProjectDto(ProjectId, name, null, 1, "S", true, null,
            new List<AddonDto>(), new List<ProjectMemberDto>());
    }

    private void SetupProject(ProjectDto? project = null)
    {
        _mediator.Send(Arg.Any<GetProjectByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(project is null
                ? Result.Fail<ProjectDto>("Проект не найден.")
                : Result.Ok(project));
        _mediator.Send(Arg.Any<GetProjectStatusesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<ProjectStatusDto>()));
    }

    private void SetupStatuses(params ProjectStatusDto[] statuses)
    {
        _mediator.Send(Arg.Any<GetProjectStatusesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(statuses.ToList()));
    }

    [Test]
    public async Task AdminStatus_ShowsConfirmationDialog()
    {
        const int statusId = 5;
        SetupProject(TestProject());
        SetupStatuses(
            new ProjectStatusDto(1, "Current", 1, true),
            new ProjectStatusDto(statusId, "New Status", 2, true));

        await _handler.HandleAdminStatusAsync(_botClient, MakeCq($"admin_status:{ProjectId}:{statusId}"), 1,
            $"admin_status:{ProjectId}:{statusId}", CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateProjectStatusCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatus_NullProject_ShowsNotFound()
    {
        SetupProject(null);

        await _handler.HandleAdminStatusAsync(_botClient, MakeCq($"admin_status:{ProjectId}:5"), 1,
            $"admin_status:{ProjectId}:5", CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<UpdateProjectStatusCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatusConfirm_UpdatesStatusAndEditsMessage()
    {
        const int statusId = 5;
        _mediator.Send(Arg.Any<UpdateProjectStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new ProjectStatusUpdateResult("P", "New Status")));

        await _handler.HandleAdminStatusConfirmAsync(_botClient, MakeCq($"admin_status_confirm:{ProjectId}:{statusId}"), 1,
            $"admin_status_confirm:{ProjectId}:{statusId}", CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateProjectStatusCommand>(c => c.ProjectId == ProjectId && c.StatusId == statusId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatusCancel_ReturnsToStatusSelection()
    {
        SetupProject(TestProject());
        SetupStatuses();

        await _handler.HandleAdminStatusCancelAsync(_botClient, MakeCq($"admin_status_cancel:{ProjectId}"), 1,
            $"admin_status_cancel:{ProjectId}", CancellationToken.None);
    }

    [Test]
    public async Task AdminStatusCancel_NullProject_ShowsNotFound()
    {
        SetupProject(null);

        await _handler.HandleAdminStatusCancelAsync(_botClient, MakeCq($"admin_status_cancel:{ProjectId}"), 1,
            $"admin_status_cancel:{ProjectId}", CancellationToken.None);
    }
}

[TestFixture]
public class AdminQuestionCallbackHandlerTests
{
    private IUserStateService _stateService;
    private IMediator _mediator;
    private AdminQuestionCallbackHandler _handler;
    private ITelegramBotClient _botClient;
    private UserSession _session;

    private static readonly Guid ProjectId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _mediator = Substitute.For<IMediator>();
        _stateService = Substitute.For<IUserStateService>();
        var authorization = Substitute.For<IAuthorizationService>();
        var menuHelper = new MainMenuHelper(authorization);
        _handler = new AdminQuestionCallbackHandler(_stateService, _mediator, menuHelper);
        _botClient = Substitute.For<ITelegramBotClient>();
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
        _mediator.Send(Arg.Any<GetProjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<ProjectDto>
            {
                new(ProjectId, "Test", null, 1, "S", true, null,
                    new List<AddonDto>(), new List<ProjectMemberDto>())
            }));
        _mediator.Send(Arg.Any<BroadcastCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<BroadcastCommand>(c => c.ProjectId == ProjectId && c.Message == messageText),
            Arg.Any<CancellationToken>());
        Assert.That(_session.PendingAction, Is.Null);
        Assert.That(_session.TempBroadcastText, Is.Null);
    }

    [Test]
    public async Task BroadcastConfirm_NullText_ReturnsEarly()
    {
        _stateService.Get(1).Returns(_session);

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<BroadcastCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BroadcastConfirm_NoProjects_ShowsError()
    {
        _session.TempBroadcastText = "text";
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<GetProjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<ProjectDto>()));

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<BroadcastCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BroadcastConfirm_NullSession_ReturnsEarly()
    {
        _stateService.Get(1).Returns((UserSession?)null);

        await _handler.HandleAdminBroadcastConfirmAsync(_botClient, MakeCq("admin_broadcast_confirm"), 1,
            CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<BroadcastCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminSelectQuestion_ShowsUsername_WhenPresent()
    {
        var qId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetUnansweredQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<QuestionDto>
            {
                new(qId, "Q?", 5, "testuser", "User1", DateTime.UtcNow, null)
            }));
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
        _mediator.Send(Arg.Any<GetUnansweredQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<QuestionDto>
            {
                new(qId, "Q?", 5, null, "User1", DateTime.UtcNow, null)
            }));
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
    private IMediator _mediator;
    private AdminProjectCallbackHandler _handler;
    private ITelegramBotClient _botClient;
    private UserSession _session;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AddonId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _mediator = Substitute.For<IMediator>();
        _stateService = Substitute.For<IUserStateService>();
        var authorization = Substitute.For<IAuthorizationService>();
        var menuHelper = new MainMenuHelper(authorization);
        var editFormBuilder = new ProjectEditFormBuilder(_mediator);
        _handler = new AdminProjectCallbackHandler(
            _stateService, _mediator, editFormBuilder, menuHelper);
        _botClient = Substitute.For<ITelegramBotClient>();
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

    private static ProjectDto TestProject()
    {
        return new ProjectDto(ProjectId, "P", null, 1, "S", true, null,
            new List<AddonDto> { new(AddonId, "A1", null, 100m) },
            new List<ProjectMemberDto>());
    }

    private void SetupProject(ProjectDto? project = null)
    {
        _mediator.Send(Arg.Any<GetProjectByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(project is null
                ? Result.Fail<ProjectDto>("Проект не найден.")
                : Result.Ok(project));
        _mediator.Send(Arg.Any<GetProjectStatusesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<ProjectStatusDto>()));
    }

    [Test]
    public async Task CreateProjectConfirm_CreatesAndRemovesSession()
    {
        _session.TempProjectName = "P";
        _session.TempProjectDescription = "D";
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<CreateProjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.NewGuid()));
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        _stateService.Received(1).Remove(1);
        await AssertMenuSentAsync(_botClient);
    }

    [Test]
    public async Task CreateProjectConfirm_NullName_ReturnsEarly()
    {
        _stateService.Get(1).Returns(_session);
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        await _mediator.DidNotReceive().Send(Arg.Any<CreateProjectCommand>(), Arg.Any<CancellationToken>());
        _botClient.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task CreateProjectConfirm_WithAddons_PassesThem()
    {
        _session.TempProjectName = "P";
        _session.TempAddons = new List<(string, string, decimal)> { ("A", "D", 10m) };
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<CreateProjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.NewGuid()));
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        _stateService.Received(1).Remove(1);
        await _mediator.Received(1).Send(
            Arg.Is<CreateProjectCommand>(c => c.Addons != null && c.Addons.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateProjectConfirm_Failure_StillReturnsToMenu()
    {
        _session.TempProjectName = "P";
        _session.TempProjectDescription = "D";
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<CreateProjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Guid>("Ошибка создания."));
        await _handler.HandleAdminCreateProjectConfirmAsync(_botClient, MakeCq("x"), 1, CancellationToken.None);
        _stateService.Received(1).Remove(1);
        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<EditMessageTextRequest>(r => r.Text != null && r.Text.Contains("Ошибка создания.")),
            Arg.Any<CancellationToken>());
        await AssertMenuSentAsync(_botClient);
    }

    private static async Task AssertMenuSentAsync(ITelegramBotClient botClient)
    {
        await botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r => r.Text == "Возвращаюсь в главное меню."),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EditProject_ShowsEditForm()
    {
        SetupProject(TestProject());
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_NullProject_ShowsNotFound()
    {
        var editFormBuilder = Substitute.For<IProjectEditFormBuilder>();
        editFormBuilder.BuildAsync(Arg.Any<Guid>(), Arg.Any<string?>())
            .Returns(Task.FromException<ProjectEditForm>(new InvalidOperationException()));
        var authorization = Substitute.For<IAuthorizationService>();
        var handler = new AdminProjectCallbackHandler(
            _stateService, _mediator, editFormBuilder, new MainMenuHelper(authorization));
        SetupProject(null);

        await handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_WithAddons_IncludesSummary()
    {
        SetupProject(TestProject());
        _stateService.GetOrCreate(1).Returns(_session);
        await _handler.HandleAdminEditProjectAsync(_botClient, MakeCq("admin_edit_project:" + ProjectId), 1,
            "admin_edit_project:" + ProjectId, CancellationToken.None);
    }

    [Test]
    public async Task EditProject_ResetsPendingAction()
    {
        _session.PendingAction = "old";
        SetupProject(TestProject());
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
        _mediator.Send(Arg.Any<GetProjectIdByAddonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(ProjectId));

        await _handler.HandleAdminDeleteAddonAsync(_botClient, MakeCq($"admin_del_addon:{addonId}"), 1,
            $"admin_del_addon:{addonId}", CancellationToken.None);
    }

    [Test]
    public async Task DeleteAddonConfirm_DeletesAndShowsResult()
    {
        var addonId = Guid.NewGuid();
        SetupProject(TestProject());
        _stateService.GetOrCreate(1).Returns(_session);
        _mediator.Send(Arg.Any<GetProjectIdByAddonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(ProjectId));
        _mediator.Send(Arg.Any<DeleteAddonCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        await _handler.HandleAdminDeleteAddonConfirmAsync(_botClient, MakeCq($"admin_del_addon_confirm:{addonId}"), 1,
            $"admin_del_addon_confirm:{addonId}", CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<DeleteAddonCommand>(c => c.AddonId == addonId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAddon_NotFound_ShowsNotFound()
    {
        var addonId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetProjectIdByAddonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Guid>("Дополнение не найдено."));

        await _handler.HandleAdminDeleteAddonConfirmAsync(_botClient, MakeCq($"admin_del_addon_confirm:{addonId}"), 1,
            $"admin_del_addon_confirm:{addonId}", CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<DeleteAddonCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAddonCancel_ReturnsToEditForm()
    {
        SetupProject(TestProject());
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
        _mediator.Send(Arg.Any<GetProjectStatusesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<ProjectStatusDto>()));
        _mediator.Send(Arg.Any<CreateProjectStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(1));

        await _handler.HandleAdminAddStatusCanEditAsync(_botClient, MakeCq("admin_add_status_can_edit:yes"), 1,
            "admin_add_status_can_edit:yes", CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<CreateProjectStatusCommand>(c => c.DisplayName == "Test Status" && c.Order == 1 && c.CanEditOrders),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddStatusCanEdit_No_CreatesStatusWithCanEditFalse()
    {
        _session.TempStatusName = "Test Status";
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<GetProjectStatusesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new List<ProjectStatusDto>()));
        _mediator.Send(Arg.Any<CreateProjectStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(2));

        await _handler.HandleAdminAddStatusCanEditAsync(_botClient, MakeCq("admin_add_status_can_edit:no"), 1,
            "admin_add_status_can_edit:no", CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<CreateProjectStatusCommand>(c => !c.CanEditOrders),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddStatusCanEdit_NullName_ReturnsEarly()
    {
        _stateService.Get(1).Returns(_session);
        await _handler.HandleAdminAddStatusCanEditAsync(_botClient, MakeCq("admin_add_status_can_edit:yes"), 1,
            "admin_add_status_can_edit:yes", CancellationToken.None);
        await _mediator.DidNotReceive().Send(Arg.Any<CreateProjectStatusCommand>(), Arg.Any<CancellationToken>());
    }
}
