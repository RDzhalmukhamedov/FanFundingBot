using System.Collections.Immutable;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Tests;

/// <summary>
/// Фабрика богатых доменных сущностей для тестов.
/// </summary>
internal static class DomainTestFactory
{
    public static User NewCustomer(long id = 1, string? username = "testuser")
        => new User(id, "Test", "User", username);

    public static User NewAdmin(long id = 1)
    {
        var admin = new User(id, "Admin", "User", "admin");
        admin.PromoteToAdmin();
        return admin;
    }

    public static ProjectStatus NewStatus(bool canEditOrders = true)
        => new ProjectStatus("Test status", 1, canEditOrders);

    public static Project NewProject(string name = "Test Project", bool canEditOrders = true)
    {
        var project = new Project();
        project.SetName(name);
        project.SetDescription("Description");
        project.SetStatus(NewStatus(canEditOrders));
        return project;
    }

    public static Project NewProjectWithId(Guid id, string name = "Test Project", bool canEditOrders = true)
        => new Project(id, name, "Description", DateTime.UtcNow, EditMode.Edit, NewStatus(canEditOrders), ImmutableArray<Addon>.Empty);

    public static Addon NewAddon(Guid? id = null, string name = "Addon", decimal price = 100m)
        => new Addon(id ?? Guid.NewGuid(), name, name, price, EditMode.Formed);

    public static DraftOrder NewDraft(
        User? customer = null,
        Project? project = null,
        Guid? addonId = null,
        int quantity = 1,
        string phone = "+71234567890",
        DeliveryMethod delivery = DeliveryMethod.Pickup,
        string? address = null)
    {
        var draft = new DraftOrder(customer ?? NewCustomer(), project ?? NewProject());
        draft.AddAddon(NewAddon(addonId ?? Guid.NewGuid()), quantity);
        draft.SetPhoneNumber(phone);
        draft.SetDeliveryMethod(delivery);
        draft.SetShippingAddress(address);
        return draft;
    }

    public static FormedOrder NewFormed(
        User? customer = null,
        Project? project = null,
        Guid? addonId = null,
        int quantity = 1,
        string phone = "+71234567890",
        DeliveryMethod delivery = DeliveryMethod.Pickup,
        string? address = null)
        => NewDraft(customer, project, addonId, quantity, phone, delivery, address).Form();

    public static Question NewQuestion(User? customer = null, string text = "Test question?")
        => new Question(customer ?? NewCustomer(), text);
}
