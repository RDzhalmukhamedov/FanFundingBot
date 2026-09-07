using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Behaviors;
using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Services;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using FluentResults;
using MediatR;
using NSubstitute;

namespace FanFundingBot.Tests.Application.Common;

[TestFixture]
public class ValidationBehaviorTests
{
    private sealed record PingCommand(string Phone) : IRequest<Result<string>>;

    private static CreateOrderCommand ValidOrderCommand()
        => new(1, Guid.NewGuid(), new Dictionary<Guid, int> { [Guid.NewGuid()] = 1 },
            "+71234567890", DeliveryMethod.Pickup, null);

    [Test]
    public async Task InvalidRequest_ShouldFailWithoutCallingHandler()
    {
        var behavior = new ValidationBehavior<CreateOrderCommand, Result<Guid>>(
            new[] { new CreateOrderCommandValidator() });
        var nextCalled = false;

        var result = await behavior.Handle(
            ValidOrderCommand() with { Phone = "bad" },
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Result.Ok(Guid.NewGuid()));
            },
            CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("телефон"));
        nextCalled.Should().BeFalse();
    }

    [Test]
    public async Task ValidRequest_ShouldCallHandler()
    {
        var behavior = new ValidationBehavior<CreateOrderCommand, Result<Guid>>(
            new[] { new CreateOrderCommandValidator() });
        var expected = Guid.NewGuid();

        var result = await behavior.Handle(
            ValidOrderCommand(),
            () => Task.FromResult(Result.Ok(expected)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Test]
    public async Task NoValidators_ShouldCallHandler()
    {
        var behavior = new ValidationBehavior<PingCommand, Result<string>>(
            Enumerable.Empty<FluentValidation.IValidator<PingCommand>>());

        var result = await behavior.Handle(
            new PingCommand("x"),
            () => Task.FromResult(Result.Ok("pong")),
            CancellationToken.None);

        result.Value.Should().Be("pong");
    }

    [Test]
    public void CreateProjectValidator_EmptyName_ShouldBeInvalid()
    {
        var validator = new CreateProjectCommandValidator();

        var result = validator.Validate(new CreateProjectCommand(1, "", "D", null));

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void AssignManagerValidator_BadIds_ShouldBeInvalid()
    {
        var validator = new AssignManagerCommandValidator();

        var result = validator.Validate(
            new AssignManagerCommand(1, Guid.Empty, 0, ProjectRole.Manager));

        result.IsValid.Should().BeFalse();
    }
}

[TestFixture]
public class TransactionBehaviorTests
{
    private sealed record PingCommand : IRequest<Result<string>>;

    [Test]
    public async Task DomainException_ShouldBecomeFailure()
    {
        var behavior = new TransactionBehavior<PingCommand, Result<string>>();

        Task<Result<string>> Next() => throw new DomainException("invariant violated");

        var result = await behavior.Handle(new PingCommand(), Next, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("invariant violated");
    }

    [Test]
    public async Task Success_ShouldPassThrough()
    {
        var behavior = new TransactionBehavior<PingCommand, Result<string>>();

        var result = await behavior.Handle(
            new PingCommand(), () => Task.FromResult(Result.Ok("ok")), CancellationToken.None);

        result.Value.Should().Be("ok");
    }

    [Test]
    public void UnexpectedException_ShouldPropagate()
    {
        var behavior = new TransactionBehavior<PingCommand, Result<string>>();

        Task<Result<string>> Next() => throw new InvalidOperationException("boom");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new PingCommand(), Next, CancellationToken.None));
    }
}

[TestFixture]
public class AuthorizationBehaviorTests
{
    private sealed record AdminOp : IRequest<Result<string>>, IRequireGlobalAdmin;
    private sealed record ProjectOp(Guid ProjectId) : IRequest<Result<string>>, IRequireProjectAccess;
    private sealed record AddonOp(Guid AddonId) : IRequest<Result<string>>, IRequireAddonAccess;
    private sealed record FreeOp : IRequest<Result<string>>;

    private ICurrentUserProvider _currentUser;
    private IAuthorizationService _authorization;
    private IAddonRepository _addons;

    [SetUp]
    public void Setup()
    {
        _currentUser = Substitute.For<ICurrentUserProvider>();
        _authorization = Substitute.For<IAuthorizationService>();
        _addons = Substitute.For<IAddonRepository>();

        _currentUser.GetCurrentUserId().Returns(1);
    }

    private AuthorizationBehavior<TRequest, Result<string>> Create<TRequest>()
        where TRequest : IRequest<Result<string>>
        => new(_currentUser, _authorization, _addons);

    private static Task<Result<string>> OkNext() => Task.FromResult(Result.Ok("ok"));

    [Test]
    public async Task GlobalAdmin_Allowed_ShouldCallHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);

        var result = await Create<AdminOp>().Handle(new AdminOp(), OkNext, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task GlobalAdmin_Denied_ShouldFail()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);

        var result = await Create<AdminOp>().Handle(new AdminOp(), OkNext, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Нет прав.");
    }

    [Test]
    public async Task ProjectAccess_Allowed_ShouldCallHandler()
    {
        var projectId = Guid.NewGuid();
        _authorization.CanManageProjectAsync(1, projectId).Returns(true);

        var result = await Create<ProjectOp>().Handle(new ProjectOp(projectId), OkNext, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task ProjectAccess_Denied_ShouldFail()
    {
        var projectId = Guid.NewGuid();
        _authorization.CanManageProjectAsync(1, projectId).Returns(false);

        var result = await Create<ProjectOp>().Handle(new ProjectOp(projectId), OkNext, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AddonAccess_UnknownAddon_ShouldFail()
    {
        _addons.GetProjectIdByAddonAsync(Arg.Any<Guid>()).Returns((Guid?)null);

        var result = await Create<AddonOp>().Handle(new AddonOp(Guid.NewGuid()), OkNext, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AddonAccess_Allowed_ShouldCallHandler()
    {
        var addonId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _addons.GetProjectIdByAddonAsync(addonId).Returns(projectId);
        _authorization.CanManageProjectAsync(1, projectId).Returns(true);

        var result = await Create<AddonOp>().Handle(new AddonOp(addonId), OkNext, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task FreeOp_ShouldCallHandlerWithoutChecks()
    {
        var result = await Create<FreeOp>().Handle(new FreeOp(), OkNext, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _authorization.DidNotReceive().CanCreateProjectAsync(Arg.Any<long>());
    }
}

[TestFixture]
public class AuthorizationServiceTests
{
    private IUserRepository _users;
    private IProjectRepository _projects;
    private AuthorizationService _service;

    [SetUp]
    public void Setup()
    {
        _users = Substitute.For<IUserRepository>();
        _projects = Substitute.For<IProjectRepository>();
        _service = new AuthorizationService(_users, _projects);
    }

    [Test]
    public async Task CanCreateProject_Admin_ShouldBeTrue()
    {
        var admin = DomainTestFactory.NewAdmin(1);
        _users.GetByIdAsync(1).Returns(admin);

        (await _service.CanCreateProjectAsync(1)).Should().BeTrue();
    }

    [Test]
    public async Task CanCreateProject_Regular_ShouldBeFalse()
    {
        _users.GetByIdAsync(1).Returns(DomainTestFactory.NewCustomer(1));

        (await _service.CanCreateProjectAsync(1)).Should().BeFalse();
    }

    [Test]
    public async Task CanCreateProject_Unknown_ShouldBeFalse()
    {
        _users.GetByIdAsync(Arg.Any<long>()).Returns((Domain.Entities.User?)null);

        (await _service.CanCreateProjectAsync(1)).Should().BeFalse();
    }

    [Test]
    public async Task CanManageProject_Member_ShouldBeTrue()
    {
        _users.GetByIdAsync(2).Returns(DomainTestFactory.NewCustomer(2));
        var project = DomainTestFactory.NewProject();
        project.AddMember(2, ProjectRole.Manager);
        _projects.GetByIdAsync(project.Id).Returns(project);

        (await _service.CanManageProjectAsync(2, project.Id)).Should().BeTrue();
    }

    [Test]
    public async Task CanManageProject_Outsider_ShouldBeFalse()
    {
        _users.GetByIdAsync(2).Returns(DomainTestFactory.NewCustomer(2));
        _projects.GetByIdAsync(Arg.Any<Guid>()).Returns(DomainTestFactory.NewProject());

        (await _service.CanManageProjectAsync(2, Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task CanAssignManager_ProjectAdminMember_ShouldBeTrue()
    {
        _users.GetByIdAsync(2).Returns(DomainTestFactory.NewCustomer(2));
        var project = DomainTestFactory.NewProject();
        project.AddMember(2, ProjectRole.ProjectAdmin);
        _projects.GetByIdAsync(project.Id).Returns(project);

        (await _service.CanAssignManagerAsync(2, project.Id)).Should().BeTrue();
    }

    [Test]
    public async Task CanAssignManager_PlainManager_ShouldBeFalse()
    {
        _users.GetByIdAsync(2).Returns(DomainTestFactory.NewCustomer(2));
        var project = DomainTestFactory.NewProject();
        project.AddMember(2, ProjectRole.Manager);
        _projects.GetByIdAsync(project.Id).Returns(project);

        (await _service.CanAssignManagerAsync(2, project.Id)).Should().BeFalse();
    }
}
