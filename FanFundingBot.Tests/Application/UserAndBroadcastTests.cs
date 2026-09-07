using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Users.Commands;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace FanFundingBot.Tests.Application;

[TestFixture]
public class UserCommandTests
{
    private IUserRepository _users;
    private GetOrCreateUserCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _users = Substitute.For<IUserRepository>();
        _handler = new GetOrCreateUserCommandHandler(_users);
    }

    [Test]
    public async Task MissingUser_ShouldCreate()
    {
        _users.GetByIdAsync(5).Returns((User?)null);

        User? saved = null;
        await _users.AddAsync(Arg.Do<User>(u => saved = u));

        var result = await _handler.Handle(
            new GetOrCreateUserCommand(5, "A", "B", "u", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(5);
        saved.Role.Should().Be(Domain.Enums.UserRole.Regular);
    }

    [Test]
    public async Task MissingOwner_ShouldCreateAsAdmin()
    {
        _users.GetByIdAsync(5).Returns((User?)null);

        User? saved = null;
        await _users.AddAsync(Arg.Do<User>(u => saved = u));

        var result = await _handler.Handle(
            new GetOrCreateUserCommand(5, "A", "B", "u", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.IsAdmin().Should().BeTrue();
    }

    [Test]
    public async Task ExistingUser_ShouldNotAdd()
    {
        _users.GetByIdAsync(5).Returns(DomainTestFactory.NewCustomer(5));

        var result = await _handler.Handle(
            new GetOrCreateUserCommand(5, "A", "B", "u", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Test]
    public async Task ExistingOwner_ShouldPromote()
    {
        var user = DomainTestFactory.NewCustomer(5);
        _users.GetByIdAsync(5).Returns(user);

        var result = await _handler.Handle(
            new GetOrCreateUserCommand(5, "A", "B", "u", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin().Should().BeTrue();
        await _users.Received(1).UpdateAsync(user);
    }
}

[TestFixture]
public class BroadcastCommandTests
{
    private IProjectRepository _projects;
    private IUserRepository _users;
    private INotificationPort _notifications;
    private BroadcastCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _projects = Substitute.For<IProjectRepository>();
        _users = Substitute.For<IUserRepository>();
        _notifications = Substitute.For<INotificationPort>();
        _handler = new BroadcastCommandHandler(_projects, _users, _notifications);
    }

    [Test]
    public async Task Happy_ShouldNotifySubscribers()
    {
        var projectId = Guid.NewGuid();
        var project = DomainTestFactory.NewProject();
        _projects.GetByIdAsync(projectId).Returns(project);
        _users.GetSubscribedToProjectAsync(projectId)
            .Returns(new List<User> { DomainTestFactory.NewCustomer(1), DomainTestFactory.NewCustomer(2) });

        var result = await _handler.Handle(
            new BroadcastCommand(1, projectId, "Hello"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _notifications.Received(1).BroadcastToProjectAsync(
            Arg.Is<IReadOnlyList<long>>(ids => ids.Count == 2),
            "Hello", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectNotFound_ShouldFail()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var result = await _handler.Handle(
            new BroadcastCommand(1, Guid.NewGuid(), "Hello"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _notifications.DidNotReceive().BroadcastToProjectAsync(
            Arg.Any<IReadOnlyList<long>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

[TestFixture]
public class ProjectDetailsQueryTests
{
    [Test]
    public async Task GetProjectById_Happy_ShouldMap()
    {
        var projects = Substitute.For<IProjectRepository>();
        var project = DomainTestFactory.NewProject("Details");
        project.AddAddon(DomainTestFactory.NewAddon(name: "A", price: 10m));
        projects.GetByIdWithAddonsAsync(project.Id).Returns(project);

        var handler = new GetProjectByIdQueryHandler(projects);
        var result = await handler.Handle(new GetProjectByIdQuery(project.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Details");
        result.Value.Addons.Should().HaveCount(1);
    }

    [Test]
    public async Task GetProjectById_NotFound_ShouldFail()
    {
        var projects = Substitute.For<IProjectRepository>();
        projects.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var handler = new GetProjectByIdQueryHandler(projects);
        var result = await handler.Handle(new GetProjectByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task GetProjectIdByAddon_Happy_ShouldReturn()
    {
        var addons = Substitute.For<IAddonRepository>();
        var projectId = Guid.NewGuid();
        addons.GetProjectIdByAddonAsync(Arg.Any<Guid>()).Returns(projectId);

        var handler = new GetProjectIdByAddonQueryHandler(addons);
        var result = await handler.Handle(new GetProjectIdByAddonQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(projectId);
    }

    [Test]
    public async Task GetProjectIdByAddon_NotFound_ShouldFail()
    {
        var addons = Substitute.For<IAddonRepository>();
        addons.GetProjectIdByAddonAsync(Arg.Any<Guid>()).Returns((Guid?)null);

        var handler = new GetProjectIdByAddonQueryHandler(addons);
        var result = await handler.Handle(new GetProjectIdByAddonQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }
}
