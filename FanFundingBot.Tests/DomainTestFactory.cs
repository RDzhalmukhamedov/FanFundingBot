using System.Collections.Immutable;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Tests;

/// <summary>
/// Фабрика богатых доменных сущностей для тестов.
/// В Фазе 1 будет расширена/пересмотрена вместе с тестами.
/// </summary>
internal static class DomainTestFactory
{
    public static Customer NewCustomer(long id = 1, string? username = "testuser")
        => new Customer(id, "Test", "User", username);

    public static Admin NewAdmin(long id = 1)
        => new Admin(id, "Admin", "User", "admin");

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
        Customer? customer = null,
        Project? project = null,
        Guid? addonId = null,
        int quantity = 1,
        string phone = "+71234567890",
        DeliveryMethod delivery = DeliveryMethod.Pickup,
        string? address = null)
    {
        var draft = new DraftOrder(customer ?? NewCustomer(), project ?? NewProject());
        if (addonId.HasValue)
            draft.AddAddon(NewAddon(addonId.Value), quantity);
        draft.SetPhoneNumber(phone);
        draft.SetDeliveryMethod(delivery);
        draft.SetShippingAddress(address);
        return draft;
    }

    public static FormedOrder NewFormed(
        Customer? customer = null,
        Project? project = null,
        Guid? addonId = null,
        int quantity = 1,
        string phone = "+71234567890",
        DeliveryMethod delivery = DeliveryMethod.Pickup,
        string? address = null)
        => NewDraft(customer, project, addonId, quantity, phone, delivery, address).Form();

    public static Question NewQuestion(Customer? customer = null, string text = "Test question?")
        => new Question(customer ?? NewCustomer(), text);
}
