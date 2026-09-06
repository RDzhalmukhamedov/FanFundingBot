using FanFundingBot.Core.Constants;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;
using FanFundingBot.Data;
using FanFundingBot.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Tests.Data;

public abstract class RepositoryTestBase
{
    protected AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        SeedStatuses(context);
        return context;
    }

    private static void SeedStatuses(AppDbContext context)
    {
        context.ProjectStatusEntries.AddRange(new List<ProjectStatus>
        {
            new() { Id = 1, DisplayName = "Запланирован", Order = 1 },
            new() { Id = 2, DisplayName = "Перевод", Order = 2 },
            new() { Id = 3, DisplayName = "Вычитка", Order = 3 },
            new() { Id = 4, DisplayName = "Подготовка макетов", Order = 4 },
            new() { Id = 5, DisplayName = "Печать", Order = 5 },
            new() { Id = 6, DisplayName = "Сортировка", Order = 6 },
            new() { Id = 7, DisplayName = "Отправка", Order = 7 },
            new() { Id = 8, DisplayName = "Завершён", Order = 8 },
            new() { Id = 9, DisplayName = "Приостановлен", Order = 9 }
        });
        context.SaveChanges();
    }
}

[TestFixture]
public class UserRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task AddAsync_ShouldAddUser()
    {
        using var context = CreateContext();
        IUserRepository repo = new UserRepository(context);

        var user = new Customer { Id = 1, FirstName = "Test", Role = UserRole.Regular, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);

        var saved = await context.Users.FindAsync(1L);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.FirstName, Is.EqualTo("Test"));
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        using var context = CreateContext();
        IUserRepository repo = new UserRepository(context);

        var result = await repo.GetByIdAsync(999);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnTrue_WhenUserExists()
    {
        using var context = CreateContext();
        IUserRepository repo = new UserRepository(context);

        await repo.AddAsync(new Customer { Id = 1, FirstName = "Test", CreatedAt = DateTime.UtcNow });

        var exists = await repo.ExistsAsync(1);
        Assert.That(exists, Is.True);
    }

    [Test]
    public async Task ExistsAsync_ShouldReturnFalse_WhenUserNotExists()
    {
        using var context = CreateContext();
        IUserRepository repo = new UserRepository(context);

        var exists = await repo.ExistsAsync(999);
        Assert.That(exists, Is.False);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateUser()
    {
        using var context = CreateContext();
        IUserRepository repo = new UserRepository(context);

        var user = new Customer { Id = 1, FirstName = "Old", Role = UserRole.Regular, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);

        user.FirstName = "Updated";
        user.Role = UserRole.Admin;
        await repo.UpdateAsync(user);

        var updated = await context.Users.FindAsync(1L);
        Assert.That(updated!.FirstName, Is.EqualTo("Updated"));
        Assert.That(updated.Role, Is.EqualTo(UserRole.Admin));
    }

    [Test]
    public async Task GetSubscribedToProjectAsync_ShouldReturnDistinctUsers()
    {
        using var context = CreateContext();
        IUserRepository userRepo = new UserRepository(context);
        IProjectRepository projectRepo = new ProjectRepository(context);
        IOrderRepository orderRepo = new OrderRepository(context);

        var user1 = new Customer { Id = 1, FirstName = "User1", CreatedAt = DateTime.UtcNow };
        var user2 = new Customer { Id = 2, FirstName = "User2", CreatedAt = DateTime.UtcNow };
        await userRepo.AddAsync(user1);
        await userRepo.AddAsync(user2);

        var project = new Project { Id = Guid.NewGuid(), Name = "P1", StatusId = 2 };
        await projectRepo.AddAsync(project);

        var order1 = new Order
        {
            Id = Guid.NewGuid(), UserId = 1, ProjectId = project.Id,
            Phone = "+71234567890", DeliveryMethod = DeliveryMethod.Pickup, Status = OrderStatus.New
        };
        var order2 = new Order
        {
            Id = Guid.NewGuid(), UserId = 2, ProjectId = project.Id,
            Phone = "+71234567891", DeliveryMethod = DeliveryMethod.Delivery, Status = OrderStatus.New
        };
        await orderRepo.AddAsync(order1);
        await orderRepo.AddAsync(order2);

        var subscribed = await userRepo.GetSubscribedToProjectAsync(project.Id);
        Assert.That(subscribed, Has.Count.EqualTo(2));
    }
}

[TestFixture]
public class ProjectRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task AddAsync_ShouldAddProject()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = new Project
        {
            Id = Guid.NewGuid(), Name = "Test Project",
            Description = "Description", StatusId = KnownProjectStatusIds.Planned
        };
        await repo.AddAsync(project);

        var saved = await repo.GetByIdAsync(project.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Name, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task GetAllWithAddonsAsync_ShouldIncludeAddons()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = new Project { Id = Guid.NewGuid(), Name = "P1", StatusId = KnownProjectStatusIds.Planned };
        project.Addons.Add(new Addon { Id = Guid.NewGuid(), Name = "A1", Price = 100 });
        project.Addons.Add(new Addon { Id = Guid.NewGuid(), Name = "A2", Price = 200 });
        await repo.AddAsync(project);

        var projects = await repo.GetAllWithAddonsAsync();
        Assert.That(projects, Has.Count.EqualTo(1));
        Assert.That(projects[0].Addons, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveProject()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = new Project { Id = Guid.NewGuid(), Name = "ToDelete", StatusId = KnownProjectStatusIds.Planned };
        await repo.AddAsync(project);

        await repo.DeleteAsync(project.Id);

        var deleted = await repo.GetByIdAsync(project.Id);
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateProject()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = new Project { Id = Guid.NewGuid(), Name = "Old", StatusId = KnownProjectStatusIds.Planned };
        await repo.AddAsync(project);

        project.Name = "Updated";
        project.StatusId = KnownProjectStatusIds.Translation;
        await repo.UpdateAsync(project);

        var updated = await repo.GetByIdAsync(project.Id);
        Assert.That(updated!.Name, Is.EqualTo("Updated"));
        Assert.That(updated.StatusId, Is.EqualTo(KnownProjectStatusIds.Translation));
    }
}

[TestFixture]
public class AddonRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task GetByProjectIdPagedAsync_ShouldReturnPagedResults()
    {
        using var context = CreateContext();
        IAddonRepository repo = new AddonRepository(context);

        var projectId = Guid.NewGuid();
        for (int i = 0; i < 10; i++)
        {
            await repo.AddAsync(new Addon
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = $"Addon {i}",
                Price = i * 10
            });
        }

        var page1 = await repo.GetByProjectIdPagedAsync(projectId, 1, 5);
        var page2 = await repo.GetByProjectIdPagedAsync(projectId, 2, 5);

        Assert.Multiple(() =>
        {
            Assert.That(page1, Has.Count.EqualTo(5));
            Assert.That(page2, Has.Count.EqualTo(5));
            Assert.That(page1[0].Name, Is.Not.EqualTo(page2[0].Name));
        });
    }

    [Test]
    public async Task CountByProjectIdAsync_ShouldReturnCorrectCount()
    {
        using var context = CreateContext();
        IAddonRepository repo = new AddonRepository(context);

        var projectId = Guid.NewGuid();
        for (int i = 0; i < 7; i++)
        {
            await repo.AddAsync(new Addon
            {
                Id = Guid.NewGuid(), ProjectId = projectId,
                Name = $"A{i}", Price = 10
            });
        }

        var count = await repo.CountByProjectIdAsync(projectId);
        Assert.That(count, Is.EqualTo(7));
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveAddon()
    {
        using var context = CreateContext();
        IAddonRepository repo = new AddonRepository(context);

        var addon = new Addon { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Name = "ToDelete", Price = 100 };
        await repo.AddAsync(addon);

        await repo.DeleteAsync(addon.Id);

        var result = await repo.GetByIdAsync(addon.Id);
        Assert.That(result, Is.Null);
    }
}

[TestFixture]
public class OrderRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task AddAsync_ShouldCreateOrder()
    {
        using var context = CreateContext();
        IOrderRepository repo = new OrderRepository(context);

        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = 1, ProjectId = Guid.NewGuid(),
            Phone = "+71234567890", DeliveryMethod = DeliveryMethod.Pickup, Status = OrderStatus.New
        };
        await repo.AddAsync(order);

        var saved = await repo.GetByIdAsync(order.Id);
        Assert.That(saved, Is.Not.Null);
    }

    [Test]
    public async Task GetByUserAsync_ShouldReturnUserOrders()
    {
        using var context = CreateContext();
        IOrderRepository repo = new OrderRepository(context);

        var projectId = Guid.NewGuid();
        for (int i = 0; i < 3; i++)
        {
            await repo.AddAsync(new Order
            {
                Id = Guid.NewGuid(), UserId = 1, ProjectId = projectId,
                Phone = "+71234567890", DeliveryMethod = DeliveryMethod.Pickup, Status = OrderStatus.New
            });
        }

        var orders = await repo.GetByUserAsync(1);
        Assert.That(orders, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetByProjectAndUserAsync_ShouldFilterCorrectly()
    {
        using var context = CreateContext();
        IOrderRepository repo = new OrderRepository(context);

        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();

        await repo.AddAsync(new Order
        {
            Id = Guid.NewGuid(), UserId = 1, ProjectId = projectId,
            Phone = "+71234567890", DeliveryMethod = DeliveryMethod.Pickup, Status = OrderStatus.New
        });
        await repo.AddAsync(new Order
        {
            Id = Guid.NewGuid(), UserId = 1, ProjectId = otherProjectId,
            Phone = "+71234567890", DeliveryMethod = DeliveryMethod.Pickup, Status = OrderStatus.New
        });

        var filtered = await repo.GetByProjectAndUserAsync(projectId, 1);
        Assert.That(filtered, Has.Count.EqualTo(1));
        Assert.That(filtered[0].ProjectId, Is.EqualTo(projectId));
    }
}

[TestFixture]
public class QuestionRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task AddAsync_ShouldCreateQuestion()
    {
        using var context = CreateContext();
        IQuestionRepository repo = new QuestionRepository(context);

        context.Users.Add(new Customer { Id = 1, FirstName = "Test", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var question = new Question
        {
            Id = Guid.NewGuid(), UserId = 1, Text = "Test question?", CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(question);

        var saved = await repo.GetByIdAsync(question.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Text, Is.EqualTo("Test question?"));
    }

    [Test]
    public async Task GetUnansweredAsync_ShouldReturnOnlyUnanswered()
    {
        using var context = CreateContext();
        IQuestionRepository repo = new QuestionRepository(context);

        context.Users.Add(new Customer { Id = 1, FirstName = "Test", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        await repo.AddAsync(new Question
        {
            Id = Guid.NewGuid(), UserId = 1, Text = "Q1", CreatedAt = DateTime.UtcNow
        });
        await repo.AddAsync(new Question
        {
            Id = Guid.NewGuid(), UserId = 1, Text = "Q2", CreatedAt = DateTime.UtcNow,
            Answer = "Answered", AnsweredAt = DateTime.UtcNow
        });

        var unanswered = await repo.GetUnansweredAsync();
        Assert.That(unanswered, Has.Count.EqualTo(1));
        Assert.That(unanswered[0].Text, Is.EqualTo("Q1"));
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateAnswer()
    {
        using var context = CreateContext();
        IQuestionRepository repo = new QuestionRepository(context);

        context.Users.Add(new Customer { Id = 1, FirstName = "Test", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var question = new Question
        {
            Id = Guid.NewGuid(), UserId = 1, Text = "Question?", CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(question);

        question.Answer = "Answer!";
        question.AnsweredAt = DateTime.UtcNow;
        await repo.UpdateAsync(question);

        var updated = await repo.GetByIdAsync(question.Id);
        Assert.That(updated!.Answer, Is.EqualTo("Answer!"));
        Assert.That(updated.AnsweredAt, Is.Not.Null);
    }
}
