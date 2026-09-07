using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace FanFundingBot.Tests.Application.Projects;

[TestFixture]
public class CreateProjectCommandTests
{
    private IProjectRepository _projects;
    private IProjectStatusRepository _statuses;
    private CreateProjectCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _projects = Substitute.For<IProjectRepository>();
        _statuses = Substitute.For<IProjectStatusRepository>();
        _handler = new CreateProjectCommandHandler(_projects, _statuses);

        _statuses.GetByIdAsync(Arg.Any<int>())
            .Returns(new ProjectStatus("Planned", 1, true));
    }

    [Test]
    public async Task Happy_ShouldCreateProjectWithAdminMember()
    {
        Project? saved = null;
        await _projects.AddAsync(Arg.Do<Project>(p => saved = p));

        var result = await _handler.Handle(
            new CreateProjectCommand(5, "P", "D",
                new List<NewAddonItem> { new("A", "DA", 10m) }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("P");
        saved.Addons.Should().HaveCount(1);
        saved.GetMemberRole(5).Should().Be(ProjectRole.ProjectAdmin);
    }

    [Test]
    public async Task NoPlannedStatus_ShouldFail()
    {
        _statuses.GetByIdAsync(Arg.Any<int>()).Returns((ProjectStatus?)null);

        var result = await _handler.Handle(
            new CreateProjectCommand(5, "P", "D", null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _projects.DidNotReceive().AddAsync(Arg.Any<Project>());
    }
}

[TestFixture]
public class UpdateProjectCommandTests
{
    private IProjectRepository _projects;

    [SetUp]
    public void Setup()
    {
        _projects = Substitute.For<IProjectRepository>();
    }

    [Test]
    public async Task Rename_Happy_ShouldUpdate()
    {
        var project = DomainTestFactory.NewProject("Old");
        _projects.GetByIdAsync(project.Id).Returns(project);

        var handler = new RenameProjectCommandHandler(_projects);
        var result = await handler.Handle(
            new RenameProjectCommand(1, project.Id, "New"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        project.Name.Should().Be("New");
        await _projects.Received(1).UpdateAsync(project);
    }

    [Test]
    public async Task Rename_NotFound_ShouldFail()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var handler = new RenameProjectCommandHandler(_projects);
        var result = await handler.Handle(
            new RenameProjectCommand(1, Guid.NewGuid(), "New"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task UpdateDescription_Happy_ShouldUpdate()
    {
        var project = DomainTestFactory.NewProject();
        _projects.GetByIdAsync(project.Id).Returns(project);

        var handler = new UpdateProjectDescriptionCommandHandler(_projects);
        var result = await handler.Handle(
            new UpdateProjectDescriptionCommand(1, project.Id, "New desc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        project.Description.Should().Be("New desc");
    }
}

[TestFixture]
public class UpdateProjectStatusCommandTests
{
    private IProjectRepository _projects;
    private IProjectStatusRepository _statuses;
    private IUserRepository _users;
    private INotificationPort _notifications;
    private UpdateProjectStatusCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _projects = Substitute.For<IProjectRepository>();
        _statuses = Substitute.For<IProjectStatusRepository>();
        _users = Substitute.For<IUserRepository>();
        _notifications = Substitute.For<INotificationPort>();
        _handler = new UpdateProjectStatusCommandHandler(_projects, _statuses, _users, _notifications);
    }

    [Test]
    public async Task Happy_ShouldUpdateAndNotify()
    {
        var project = DomainTestFactory.NewProject();
        var status = new ProjectStatus("New", 2, false);
        _projects.GetByIdAsync(project.Id).Returns(project);
        _statuses.GetByIdAsync(2).Returns(status);

        var subscriber = DomainTestFactory.NewCustomer(7);
        _users.GetSubscribedToProjectAsync(project.Id).Returns(new List<User> { subscriber });

        var result = await _handler.Handle(
            new UpdateProjectStatusCommand(1, project.Id, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(status);
        await _notifications.Received(1).NotifyProjectStatusChangedAsync(
            Arg.Is<IReadOnlyList<long>>(ids => ids.Contains(7)),
            project.Name, "New", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectNotFound_ShouldFail()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var result = await _handler.Handle(
            new UpdateProjectStatusCommand(1, Guid.NewGuid(), 2), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _notifications.DidNotReceive().NotifyProjectStatusChangedAsync(
            Arg.Any<IReadOnlyList<long>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StatusNotFound_ShouldFail()
    {
        var project = DomainTestFactory.NewProject();
        _projects.GetByIdAsync(project.Id).Returns(project);
        _statuses.GetByIdAsync(Arg.Any<int>()).Returns((ProjectStatus?)null);

        var result = await _handler.Handle(
            new UpdateProjectStatusCommand(1, project.Id, 99), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }
}

[TestFixture]
public class AddonCommandTests
{
    private IProjectRepository _projects;
    private IAddonRepository _addons;

    [SetUp]
    public void Setup()
    {
        _projects = Substitute.For<IProjectRepository>();
        _addons = Substitute.For<IAddonRepository>();
    }

    [Test]
    public async Task CreateAddon_Happy_ShouldAddToProject()
    {
        var project = DomainTestFactory.NewProject();
        _projects.GetByIdWithAddonsAsync(project.Id).Returns(project);

        var handler = new CreateAddonCommandHandler(_projects);
        var result = await handler.Handle(
            new CreateAddonCommand(1, project.Id, "A", "DA", 15m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        project.Addons.Should().HaveCount(1);
        project.Addons[0].Name.Should().Be("A");
        await _projects.Received(1).UpdateAsync(project);
    }

    [Test]
    public async Task CreateAddon_ProjectNotFound_ShouldFail()
    {
        _projects.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var handler = new CreateAddonCommandHandler(_projects);
        var result = await handler.Handle(
            new CreateAddonCommand(1, Guid.NewGuid(), "A", "DA", 15m), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAddonName_Happy_ShouldUpdate()
    {
        var addon = DomainTestFactory.NewAddon();
        _addons.GetByIdAsync(addon.Id).Returns(addon);

        var handler = new UpdateAddonNameCommandHandler(_addons);
        var result = await handler.Handle(
            new UpdateAddonNameCommand(1, addon.Id, "Renamed"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addon.Name.Should().Be("Renamed");
    }

    [Test]
    public async Task UpdateAddonPrice_Happy_ShouldUpdate()
    {
        var addon = DomainTestFactory.NewAddon();
        _addons.GetByIdAsync(addon.Id).Returns(addon);

        var handler = new UpdateAddonPriceCommandHandler(_addons);
        var result = await handler.Handle(
            new UpdateAddonPriceCommand(1, addon.Id, 42m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ((decimal)addon.Price).Should().Be(42m);
    }

    [Test]
    public async Task DeleteAddon_Happy_ShouldDelete()
    {
        var addon = DomainTestFactory.NewAddon();
        _addons.GetByIdAsync(addon.Id).Returns(addon);
        _addons.HasOrdersAsync(addon.Id).Returns(false);

        var handler = new DeleteAddonCommandHandler(_addons);
        var result = await handler.Handle(
            new DeleteAddonCommand(1, addon.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _addons.Received(1).DeleteAsync(addon.Id);
    }

    [Test]
    public async Task DeleteAddon_HasOrders_ShouldFail()
    {
        var addon = DomainTestFactory.NewAddon();
        _addons.GetByIdAsync(addon.Id).Returns(addon);
        _addons.HasOrdersAsync(addon.Id).Returns(true);

        var handler = new DeleteAddonCommandHandler(_addons);
        var result = await handler.Handle(
            new DeleteAddonCommand(1, addon.Id), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _addons.DidNotReceive().DeleteAsync(Arg.Any<Guid>());
    }
}

[TestFixture]
public class StatusAndMemberCommandTests
{
    [Test]
    public async Task CreateProjectStatus_Happy_ShouldAdd()
    {
        var statuses = Substitute.For<IProjectStatusRepository>();
        ProjectStatus? saved = null;
        await statuses.AddAsync(Arg.Do<ProjectStatus>(s => saved = s));

        var handler = new CreateProjectStatusCommandHandler(statuses);
        var result = await handler.Handle(
            new CreateProjectStatusCommand(1, "Draft", 3, true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.DisplayName.Should().Be("Draft");
    }

    [Test]
    public async Task AssignManager_Happy_ShouldAddMember()
    {
        var projects = Substitute.For<IProjectRepository>();
        var users = Substitute.For<IUserRepository>();
        var auth = Substitute.For<IAuthorizationService>();

        var project = DomainTestFactory.NewProject();
        projects.GetByIdAsync(project.Id).Returns(project);
        users.GetByIdAsync(9).Returns(DomainTestFactory.NewCustomer(9));
        auth.CanAssignManagerAsync(1, project.Id).Returns(true);

        var handler = new AssignManagerCommandHandler(projects, users, auth);
        var result = await handler.Handle(
            new AssignManagerCommand(1, project.Id, 9, ProjectRole.Manager), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        project.GetMemberRole(9).Should().Be(ProjectRole.Manager);
        await projects.Received(1).UpdateAsync(project);
    }

    [Test]
    public async Task AssignManager_NoRights_ShouldFail()
    {
        var projects = Substitute.For<IProjectRepository>();
        var users = Substitute.For<IUserRepository>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.CanAssignManagerAsync(Arg.Any<long>(), Arg.Any<Guid>()).Returns(false);

        var handler = new AssignManagerCommandHandler(projects, users, auth);
        var result = await handler.Handle(
            new AssignManagerCommand(2, Guid.NewGuid(), 9, ProjectRole.Manager), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await projects.DidNotReceive().UpdateAsync(Arg.Any<Project>());
    }

    [Test]
    public async Task AssignManager_DuplicateMember_ShouldThrowDomainException()
    {
        var projects = Substitute.For<IProjectRepository>();
        var users = Substitute.For<IUserRepository>();
        var auth = Substitute.For<IAuthorizationService>();

        var project = DomainTestFactory.NewProject();
        project.AddMember(9, ProjectRole.Manager);
        projects.GetByIdAsync(project.Id).Returns(project);
        users.GetByIdAsync(9).Returns(DomainTestFactory.NewCustomer(9));
        auth.CanAssignManagerAsync(1, project.Id).Returns(true);

        var handler = new AssignManagerCommandHandler(projects, users, auth);

        Assert.ThrowsAsync<DomainException>(async () => await handler.Handle(
            new AssignManagerCommand(1, project.Id, 9, ProjectRole.Manager), CancellationToken.None));
    }
}

[TestFixture]
public class ProjectQueryTests
{
    [Test]
    public async Task GetProjects_ShouldMapDtos()
    {
        var projects = Substitute.For<IProjectRepository>();
        var project = DomainTestFactory.NewProject("Showcase");
        project.AddAddon(DomainTestFactory.NewAddon(name: "A1", price: 50m));
        project.AddMember(3, ProjectRole.Manager);
        projects.GetAllWithAddonsAsync().Returns(new List<Project> { project });

        var handler = new GetProjectsQueryHandler(projects);
        var result = await handler.Handle(new GetProjectsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Should().ContainSingle().Subject;
        dto.Name.Should().Be("Showcase");
        dto.StatusName.Should().Be("Test status");
        dto.Addons.Should().HaveCount(1);
        dto.Addons[0].Price.Should().Be(50m);
        dto.Members.Should().HaveCount(1);
    }

    [Test]
    public async Task GetProjectStatuses_ShouldMapDtos()
    {
        var statuses = Substitute.For<IProjectStatusRepository>();
        statuses.GetAllAsync().Returns(new List<ProjectStatus>
        {
            new("Planned", 1, true)
        });

        var handler = new GetProjectStatusesQueryHandler(statuses);
        var result = await handler.Handle(new GetProjectStatusesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.DisplayName.Should().Be("Planned");
    }
}
