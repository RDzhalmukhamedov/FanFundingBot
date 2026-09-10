using FanFundingBot.Application;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FanFundingBot.Tests.Application;

[TestFixture]
public class DependencyInjectionTests
{
    [Test]
    public async Task MediatorPipeline_ShouldResolveAndExecute()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var users = Substitute.For<IUserRepository>();
        var projects = Substitute.For<IProjectRepository>();
        var orders = Substitute.For<IOrderRepository>();
        var questions = Substitute.For<IQuestionRepository>();
        var statuses = Substitute.For<IProjectStatusRepository>();
        var notifications = Substitute.For<INotificationPort>();
        var currentUser = Substitute.For<ICurrentUserProvider>();

        services.AddScoped(_ => users);
        services.AddScoped(_ => projects);
        services.AddScoped(_ => orders);
        services.AddScoped(_ => questions);
        services.AddScoped(_ => statuses);
        services.AddScoped(_ => notifications);
        services.AddScoped(_ => currentUser);
        services.AddScoped<IUnitOfWork>(_ => new Common.PassThroughUnitOfWork());

        currentUser.GetCurrentUserId().Returns(1);

        var customer = DomainTestFactory.NewCustomer(1);
        customer.SetPhone("+71234567890");
        users.GetByIdAsync(1).Returns(customer);
        orders.GetByProjectAndUserAsync(Arg.Any<Guid>(), Arg.Any<long>())
            .Returns(new List<Domain.Entities.Order>());

        var project = DomainTestFactory.NewProject("Pipeline");
        var addonId = Guid.NewGuid();
        project.AddAddon(DomainTestFactory.NewAddon(addonId, "A", 25m));
        projects.GetByIdWithAddonsAsync(project.Id).Returns(project);
        projects.GetAllWithAddonsAsync().Returns(new List<Domain.Entities.Project> { project });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var createResult = await mediator.Send(new CreateOrderCommand(1, project.Id,
            new Dictionary<Guid, int> { [addonId] = 1 },
            "+71234567890", DeliveryMethod.Pickup, null));

        createResult.IsSuccess.Should().BeTrue();

        var projectsResult = await mediator.Send(new GetProjectsQuery());

        projectsResult.IsSuccess.Should().BeTrue();
        var dto = projectsResult.Value.Should().ContainSingle().Subject;
        dto.Name.Should().Be("Pipeline");
        dto.Addons.Should().HaveCount(1);
        dto.Addons[0].Price.Should().Be(25m);
    }
}
