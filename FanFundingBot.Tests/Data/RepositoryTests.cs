using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Domain.ValueObjects;
using FanFundingBot.Infrastructure.Persistence;
using FanFundingBot.Infrastructure.Persistence.Repositories;
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
        context.ProjectStatusEntries.AddRange(
            new ProjectStatus("Запланирован", 1, true),
            new ProjectStatus("Перевод", 2, true),
            new ProjectStatus("Вычитка", 3, true),
            new ProjectStatus("Подготовка макетов", 4, true),
            new ProjectStatus("Печать", 5, true),
            new ProjectStatus("Сортировка", 6, true),
            new ProjectStatus("Отправка", 7, true),
            new ProjectStatus("Завершён", 8, false),
            new ProjectStatus("Приостановлен", 9, false));
        context.SaveChanges();
    }

    protected static ProjectStatus GetStatus(AppDbContext context, int order)
        => context.ProjectStatusEntries.First(s => s.Order == order);

    protected static Project NewProject(AppDbContext context, string name = "P1", int statusOrder = 1)
    {
        var project = new Project();
        project.SetName(name);
        project.SetDescription("Description");
        project.SetStatus(GetStatus(context, statusOrder));
        return project;
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

        var user = DomainTestFactory.NewCustomer(1);
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

        await repo.AddAsync(DomainTestFactory.NewCustomer(1));

        await context.SaveChangesAsync();
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

        var user = DomainTestFactory.NewCustomer(1);
        await repo.AddAsync(user);
        await context.SaveChangesAsync();

        user.SetPhone("+79990001122");
        user.PromoteToAdmin();
        await repo.UpdateAsync(user);
        await context.SaveChangesAsync();

        var updated = await context.Users.FindAsync(1L);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Phone, Is.EqualTo("+79990001122"));
            Assert.That(updated.Role, Is.EqualTo(UserRole.Admin));
        });
    }

    [Test]
    public async Task GetSubscribedToProjectAsync_ShouldReturnDistinctUsers()
    {
        using var context = CreateContext();
        IUserRepository userRepo = new UserRepository(context);
        IProjectRepository projectRepo = new ProjectRepository(context);
        IOrderRepository orderRepo = new OrderRepository(context);

        var user1 = DomainTestFactory.NewCustomer(1);
        var user2 = DomainTestFactory.NewCustomer(2);
        await userRepo.AddAsync(user1);
        await userRepo.AddAsync(user2);

        var project = NewProject(context);
        await projectRepo.AddAsync(project);

        await orderRepo.AddAsync(DomainTestFactory.NewFormed(user1, project));
        await orderRepo.AddAsync(DomainTestFactory.NewFormed(user2, project));
        await context.SaveChangesAsync();

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

        var project = NewProject(context, "Test Project");
        await repo.AddAsync(project);
        await context.SaveChangesAsync();

        var saved = await repo.GetByIdAsync(project.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Name, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task GetAllWithAddonsAsync_ShouldIncludeAddons()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = NewProject(context);
        project.AddAddon(DomainTestFactory.NewAddon(name: "A1", price: 100));
        project.AddAddon(DomainTestFactory.NewAddon(name: "A2", price: 200));
        await repo.AddAsync(project);
        await context.SaveChangesAsync();

        var projects = await repo.GetAllWithAddonsAsync();
        Assert.That(projects, Has.Count.EqualTo(1));
        Assert.That(projects[0].Addons, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveProject()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = NewProject(context, "ToDelete");
        await repo.AddAsync(project);
        await context.SaveChangesAsync();

        await repo.DeleteAsync(project.Id);
        await context.SaveChangesAsync();

        var deleted = await repo.GetByIdAsync(project.Id);
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateProject()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = NewProject(context, "Old");
        await repo.AddAsync(project);
        await context.SaveChangesAsync();

        project.StartEdit();
        project.SetName("Updated");
        project.SetStatus(GetStatus(context, 2));
        project.FinishEdit();
        await repo.UpdateAsync(project);
        await context.SaveChangesAsync();

        var updated = await repo.GetByIdAsync(project.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Name, Is.EqualTo("Updated"));
            Assert.That(updated.Status.Order, Is.EqualTo(2));
        });
    }
}

[TestFixture]
public class ProjectAddonTests : RepositoryTestBase
{
    [Test]
    public async Task UpdateAsync_ShouldRemoveAddon_WhenRemovedFromProject()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = NewProject(context);
        var addon = DomainTestFactory.NewAddon(name: "A1", price: 100);
        project.AddAddon(addon);
        await repo.AddAsync(project);
        await context.SaveChangesAsync();

        project.StartEdit();
        project.RemoveAddon(addon.Id, hasOrders: false);
        project.FinishEdit();
        await repo.UpdateAsync(project);
        await context.SaveChangesAsync();

        var reloaded = await repo.GetByIdWithAddonsAsync(project.Id);
        Assert.That(reloaded!.Addons, Is.Empty);
        Assert.That(await context.Addons.FindAsync(addon.Id), Is.Null);
    }

    [Test]
    public async Task GetProjectIdByAddonAsync_ShouldReturnProjectId()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var project = NewProject(context);
        var addon = DomainTestFactory.NewAddon(name: "A1", price: 100);
        project.AddAddon(addon);
        await repo.AddAsync(project);
        await context.SaveChangesAsync();

        var projectId = await repo.GetProjectIdByAddonAsync(addon.Id);
        Assert.That(projectId, Is.EqualTo(project.Id));
    }

    [Test]
    public async Task GetProjectIdByAddonAsync_ShouldReturnNull_WhenNotFound()
    {
        using var context = CreateContext();
        IProjectRepository repo = new ProjectRepository(context);

        var projectId = await repo.GetProjectIdByAddonAsync(Guid.NewGuid());
        Assert.That(projectId, Is.Null);
    }
}

[TestFixture]
public class OrderRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task AddAsync_ShouldCreateOrder()
    {
        using var context = CreateContext();
        IUserRepository userRepo = new UserRepository(context);
        IProjectRepository projectRepo = new ProjectRepository(context);
        IOrderRepository repo = new OrderRepository(context);

        var user = DomainTestFactory.NewCustomer(1);
        await userRepo.AddAsync(user);
        var project = NewProject(context);
        await projectRepo.AddAsync(project);

        var order = DomainTestFactory.NewFormed(user, project);
        await repo.AddAsync(order);
        await context.SaveChangesAsync();

        var saved = await repo.GetByIdAsync(order.Id);
        Assert.That(saved, Is.Not.Null);
    }

    [Test]
    public async Task GetByUserAsync_ShouldReturnUserOrders()
    {
        using var context = CreateContext();
        IUserRepository userRepo = new UserRepository(context);
        IProjectRepository projectRepo = new ProjectRepository(context);
        IOrderRepository repo = new OrderRepository(context);

        var user = DomainTestFactory.NewCustomer(1);
        await userRepo.AddAsync(user);
        var project = NewProject(context);
        await projectRepo.AddAsync(project);

        for (int i = 0; i < 3; i++)
            await repo.AddAsync(DomainTestFactory.NewFormed(user, project));
        await context.SaveChangesAsync();

        var orders = await repo.GetByUserAsync(1);
        Assert.That(orders, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetByProjectAndUserAsync_ShouldFilterCorrectly()
    {
        using var context = CreateContext();
        IUserRepository userRepo = new UserRepository(context);
        IProjectRepository projectRepo = new ProjectRepository(context);
        IOrderRepository repo = new OrderRepository(context);

        var user = DomainTestFactory.NewCustomer(1);
        await userRepo.AddAsync(user);
        var project = NewProject(context);
        var otherProject = NewProject(context, "Other");
        await projectRepo.AddAsync(project);
        await projectRepo.AddAsync(otherProject);

        await repo.AddAsync(DomainTestFactory.NewFormed(user, project));
        await repo.AddAsync(DomainTestFactory.NewFormed(user, otherProject));
        await context.SaveChangesAsync();

        var filtered = await repo.GetByProjectAndUserAsync(project.Id, 1);
        Assert.That(filtered, Has.Count.EqualTo(1));
        Assert.That(filtered[0].Project.Id, Is.EqualTo(project.Id));
    }

    [Test]
    public async Task GetByIdWithAddonsAsync_ShouldIncludePositions()
    {
        using var context = CreateContext();
        IUserRepository userRepo = new UserRepository(context);
        IProjectRepository projectRepo = new ProjectRepository(context);
        IOrderRepository repo = new OrderRepository(context);

        var user = DomainTestFactory.NewCustomer(1);
        await userRepo.AddAsync(user);
        var project = NewProject(context);
        var addon = DomainTestFactory.NewAddon(Guid.NewGuid(), "A1", 50);
        project.AddAddon(addon);
        await projectRepo.AddAsync(project);

        var draft = new DraftOrder(user, project);
        draft.AddAddon(addon, 2);
        draft.SetPhoneNumber("+71234567890");
        draft.SetDeliveryMethod(DeliveryMethod.Pickup);
        var order = draft.Form();
        await repo.AddAsync(order);
        await context.SaveChangesAsync();

        var loaded = await repo.GetByIdWithAddonsAsync(order.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.OrderAddons, Has.Count.EqualTo(1));
            Assert.That(loaded.OrderAddons[0].Quantity, Is.EqualTo(Quantity.Create(2)));
            Assert.That(loaded.OrderAddons[0].Addon.Name, Is.EqualTo("A1"));
            Assert.That(loaded.User.Id, Is.EqualTo(1));
        });
    }
}

[TestFixture]
public class QuestionRepositoryTests : RepositoryTestBase
{
    private static async Task<User> AddCustomerAsync(AppDbContext context)
    {
        var customer = DomainTestFactory.NewCustomer(1);
        context.Users.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    [Test]
    public async Task AddAsync_ShouldCreateQuestion()
    {
        using var context = CreateContext();
        IQuestionRepository repo = new QuestionRepository(context);

        var customer = await AddCustomerAsync(context);

        var question = new Question(customer, "Test question?");
        await repo.AddAsync(question);
        await context.SaveChangesAsync();

        var saved = await repo.GetByIdAsync(question.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Text, Is.EqualTo("Test question?"));
    }

    [Test]
    public async Task GetUnansweredAsync_ShouldReturnOnlyUnanswered()
    {
        using var context = CreateContext();
        IQuestionRepository repo = new QuestionRepository(context);

        var customer = await AddCustomerAsync(context);
        var admin = DomainTestFactory.NewAdmin(2);
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        await repo.AddAsync(new Question(customer, "Q1"));

        var answered = new Question(customer, "Q2");
        answered.AnswerQuestion(admin, "Answered");
        await repo.AddAsync(answered);
        await context.SaveChangesAsync();

        var unanswered = await repo.GetUnansweredAsync();
        Assert.That(unanswered, Has.Count.EqualTo(1));
        Assert.That(unanswered[0].Text, Is.EqualTo("Q1"));
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateAnswer()
    {
        using var context = CreateContext();
        IQuestionRepository repo = new QuestionRepository(context);

        var customer = await AddCustomerAsync(context);
        var admin = DomainTestFactory.NewAdmin(2);
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var question = new Question(customer, "Question?");
        await repo.AddAsync(question);
        await context.SaveChangesAsync();

        question.AnswerQuestion(admin, "Answer!");
        await repo.UpdateAsync(question);
        await context.SaveChangesAsync();

        var updated = await repo.GetByIdAsync(question.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Answer, Is.EqualTo("Answer!"));
            Assert.That(updated.AnsweredAt, Is.Not.Null);
        });
    }
}
