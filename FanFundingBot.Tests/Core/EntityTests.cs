using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.ValueObjects;

namespace FanFundingBot.Tests.Core;

[TestFixture]
public class EntityTests
{
    [Test]
    public void Project_NewProject_ShouldHaveDefaults()
    {
        var project = new Project();

        Assert.Multiple(() =>
        {
            Assert.That(project.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(project.Name, Is.EqualTo(string.Empty));
            Assert.That(project.Addons, Is.Empty);
            Assert.That(project.Members, Is.Empty);
            Assert.That(project.Mode, Is.EqualTo(EditMode.Edit));
        });
    }

    [Test]
    public void Project_SetNameAndDescription_ShouldUpdate()
    {
        var project = new Project();
        project.SetName("Test Project");
        project.SetDescription("Test Description");

        Assert.Multiple(() =>
        {
            Assert.That(project.Name, Is.EqualTo("Test Project"));
            Assert.That(project.Description, Is.EqualTo("Test Description"));
        });
    }

    [Test]
    public void Project_SetName_Empty_ShouldThrow()
    {
        var project = new Project();

        Assert.Throws<ArgumentException>(() => project.SetName(""));
    }

    [Test]
    public void Project_FinishEdit_ShouldSetFormedAndCreatedAt()
    {
        var project = new Project();
        project.SetName("P");
        project.SetDescription("D");
        project.SetStatus(new ProjectStatus("S", 1, true));

        project.FinishEdit();

        Assert.Multiple(() =>
        {
            Assert.That(project.Mode, Is.EqualTo(EditMode.Formed));
            Assert.That(project.CreatedAt, Is.Not.Null);
            Assert.That(project.CanEditOrders, Is.True);
        });
    }

    [Test]
    public void Project_SetName_AfterFinishEdit_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();
        project.FinishEdit();

        Assert.Throws<ProjectNotEditModeException>(() => project.SetName("New"));
    }

    [Test]
    public void Project_AddAddon_Duplicate_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();

        project.AddAddon(addon);

        Assert.Throws<DomainException>(() => project.AddAddon(addon));
    }

    [Test]
    public void Project_SetStatus_ShouldRaiseEvent()
    {
        var project = DomainTestFactory.NewProject();
        project.ClearDomainEvents();

        project.SetStatus(new ProjectStatus("New", 2, false));

        Assert.That(project.DomainEvents.OfType<ProjectStatusChangedEvent>(), Has.Exactly(1).Items);
    }

    [Test]
    public void Project_AddMember_Duplicate_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();

        project.AddMember(1, ProjectRole.Manager);

        Assert.Multiple(() =>
        {
            Assert.That(project.IsMember(1), Is.True);
            Assert.That(project.GetMemberRole(1), Is.EqualTo(ProjectRole.Manager));
            Assert.Throws<DomainException>(() => project.AddMember(1, ProjectRole.Manager));
        });
    }

    [Test]
    public void Project_RemoveMember_Missing_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();

        Assert.Throws<DomainException>(() => project.RemoveMember(999));
    }

    [Test]
    public void Project_AddMember_ShouldRaiseEvent()
    {
        var project = DomainTestFactory.NewProject();

        project.AddMember(5, ProjectRole.ProjectAdmin);

        var evt = project.DomainEvents.OfType<ProjectMemberAddedEvent>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(evt.UserId, Is.EqualTo(5));
            Assert.That(evt.Role, Is.EqualTo(ProjectRole.ProjectAdmin));
        });
    }

    [Test]
    public void Addon_SetPrice_Negative_ShouldThrow()
    {
        var addon = new Addon();

        Assert.Throws<DomainException>(() => addon.SetPrice(-1));
    }

    [Test]
    public void Addon_SetAfterFinishEdit_ShouldThrow()
    {
        var addon = DomainTestFactory.NewAddon();
        addon.StartEdit();
        addon.FinishEdit();

        Assert.Throws<AddonNotEditModeException>(() => addon.SetName("New"));
    }

    [Test]
    public void User_NewUser_ShouldHaveRegularRole()
    {
        var user = new User(12345, "John", "Doe", "johndoe");

        Assert.Multiple(() =>
        {
            Assert.That(user.Id, Is.EqualTo(12345));
            Assert.That(user.FirstName, Is.EqualTo("John"));
            Assert.That(user.LastName, Is.EqualTo("Doe"));
            Assert.That(user.Username, Is.EqualTo("johndoe"));
            Assert.That(user.Role, Is.EqualTo(UserRole.Regular));
            Assert.That(user.IsAdmin(), Is.False);
        });
    }

    [Test]
    public void User_PromoteToAdmin_ShouldChangeRole()
    {
        var user = new User(1, "John", "Doe", "johndoe");
        user.PromoteToAdmin();

        Assert.Multiple(() =>
        {
            Assert.That(user.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(user.IsAdmin(), Is.True);
        });
    }

    [Test]
    public void User_SetPhone_ShouldUpdate()
    {
        var user = new User(1, "John", "Doe", "johndoe");
        user.SetPhone("+71234567890");

        Assert.That(user.Phone, Is.EqualTo("+71234567890"));
    }

    [Test]
    public void User_StartOrdering_DuplicateProject_ShouldThrow()
    {
        var customer = DomainTestFactory.NewCustomer();
        var project = DomainTestFactory.NewProject();

        customer.StartOrdering(project);

        Assert.Throws<DomainException>(() => customer.StartOrdering(project));
    }

    [Test]
    public void User_FormAndEditOrder_ShouldChangeState()
    {
        var customer = DomainTestFactory.NewCustomer();
        var project = DomainTestFactory.NewProject();

        var draft = customer.StartOrdering(project);
        draft.AddAddon(DomainTestFactory.NewAddon());
        draft.SetPhoneNumber("+71234567890");
        var formed = customer.FormOrder(draft.Id);

        Assert.That(formed, Is.InstanceOf<FormedOrder>());

        var edited = customer.EditOrder(draft.Id);

        Assert.That(edited, Is.InstanceOf<DraftOrder>());
    }

    [Test]
    public void DraftOrder_Form_ShouldCarryStateAndRaiseEvent()
    {
        var addonId = Guid.NewGuid();
        var draft = DomainTestFactory.NewDraft(addonId: addonId, quantity: 2);

        var formed = draft.Form();

        Assert.Multiple(() =>
        {
            Assert.That(formed.Id, Is.EqualTo(draft.Id));
            Assert.That(formed.Phone.ToString(), Is.EqualTo(draft.Phone.ToString()));
            Assert.That(formed.OrderAddons, Has.Count.EqualTo(1));
            Assert.That(formed.OrderAddons[0].Addon.Id, Is.EqualTo(addonId));
            Assert.That((int)formed.OrderAddons[0].Quantity, Is.EqualTo(2));
            Assert.That(formed.DomainEvents.OfType<OrderFormedEvent>(), Has.Exactly(1).Items);
        });
    }

    [Test]
    public void DraftOrder_Form_Empty_ShouldThrow()
    {
        var draft = new DraftOrder(DomainTestFactory.NewCustomer(), DomainTestFactory.NewProject());
        draft.SetPhoneNumber("+71234567890");

        Assert.Throws<DomainException>(() => draft.Form());
    }

    [Test]
    public void DraftOrder_Form_WithoutPhone_ShouldThrow()
    {
        var draft = new DraftOrder(DomainTestFactory.NewCustomer(), DomainTestFactory.NewProject());
        draft.AddAddon(DomainTestFactory.NewAddon());

        Assert.Throws<DomainException>(() => draft.Form());
    }

    [Test]
    public void DraftOrder_Form_DeliveryWithoutAddress_ShouldThrow()
    {
        var draft = new DraftOrder(DomainTestFactory.NewCustomer(), DomainTestFactory.NewProject());
        draft.AddAddon(DomainTestFactory.NewAddon());
        draft.SetPhoneNumber("+71234567890");
        draft.SetDeliveryMethod(DeliveryMethod.Delivery);

        Assert.Throws<DomainException>(() => draft.Form());
    }

    [Test]
    public void DraftOrder_SetPhone_Invalid_ShouldThrow()
    {
        var draft = DomainTestFactory.NewDraft();

        Assert.Throws<DomainException>(() => draft.SetPhoneNumber("bad"));
    }

    [Test]
    public void DraftOrder_AddDuplicateAddon_ShouldThrow()
    {
        var draft = DomainTestFactory.NewDraft();
        var addon = DomainTestFactory.NewAddon();

        draft.AddAddon(addon);

        Assert.Throws<DomainException>(() => draft.AddAddon(addon));
    }

    [Test]
    public void DraftOrder_IncrementDecrement_ShouldChangeQuantity()
    {
        var addonId = Guid.NewGuid();
        var draft = DomainTestFactory.NewDraft(addonId: addonId);

        Assert.That(draft.IncrementAddonQuantity(addonId), Is.EqualTo(2));
        Assert.That(draft.DecrementAddonQuantity(addonId), Is.EqualTo(1));
    }

    [Test]
    public void DraftOrder_DecrementBelowOne_ShouldThrow()
    {
        var addonId = Guid.NewGuid();
        var draft = DomainTestFactory.NewDraft(addonId: addonId);

        Assert.Throws<DomainException>(() => draft.DecrementAddonQuantity(addonId));
    }

    [Test]
    public void DraftOrder_IncrementMissingAddon_ShouldThrow()
    {
        var draft = DomainTestFactory.NewDraft();

        Assert.Throws<DomainException>(() => draft.IncrementAddonQuantity(Guid.NewGuid()));
    }

    [Test]
    public void FormedOrder_Mutating_ShouldThrow()
    {
        var formed = DomainTestFactory.NewFormed();

        Assert.Throws<OrderNotEditModeException>(() => formed.AddAddon(DomainTestFactory.NewAddon()));
        Assert.Throws<OrderNotEditModeException>(() => formed.Form());
    }

    [Test]
    public void FormedOrder_Edit_ShouldReturnDraftWithState()
    {
        var formed = DomainTestFactory.NewFormed(phone: "+79998887766");

        var draft = formed.Edit();

        Assert.Multiple(() =>
        {
            Assert.That(draft.Id, Is.EqualTo(formed.Id));
            Assert.That(draft.Phone.ToString(), Is.EqualTo("+79998887766"));
            Assert.That(draft.OrderAddons, Has.Count.EqualTo(formed.OrderAddons.Count));
        });
    }

    [Test]
    public void PhoneNumber_Create_Invalid_ShouldThrow()
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create("123"));
        Assert.Throws<DomainException>(() => PhoneNumber.Create(""));
        Assert.Throws<DomainException>(() => PhoneNumber.Create(null));
    }

    [Test]
    public void PhoneNumber_Valid_ShouldConvertToString()
    {
        PhoneNumber phone = PhoneNumber.Create("+71234567890");

        Assert.Multiple(() =>
        {
            Assert.That(phone.ToString(), Is.EqualTo("+71234567890"));
            Assert.That(phone.IsEmpty, Is.False);
            Assert.That(PhoneNumber.Empty.IsEmpty, Is.True);
        });
    }

    [Test]
    public void Address_Create_Empty_ShouldThrow()
    {
        Assert.Throws<DomainException>(() => Address.Create("  "));
    }

    [Test]
    public void Money_Create_Negative_ShouldThrow()
    {
        Assert.Throws<DomainException>(() => Money.Create(-0.01m));
    }

    [Test]
    public void Money_Operators_ShouldCalculate()
    {
        var total = Money.Create(100m) + Money.Create(50m);
        var line = Money.Create(100m) * 3;

        Assert.Multiple(() =>
        {
            Assert.That((decimal)total, Is.EqualTo(150m));
            Assert.That((decimal)line, Is.EqualTo(300m));
        });
    }

    [Test]
    public void Quantity_Create_OutOfRange_ShouldThrow()
    {
        Assert.Throws<DomainException>(() => Quantity.Create(0));
        Assert.Throws<DomainException>(() => Quantity.Create(101));
    }

    [Test]
    public void OrderPosition_InvalidQuantity_ShouldThrow()
    {
        var addon = DomainTestFactory.NewAddon();

        Assert.Throws<DomainException>(() => new OrderPosition(addon, 0));
    }

    [Test]
    public void OrderPosition_PlusOverLimit_ShouldThrow()
    {
        var position = new OrderPosition(DomainTestFactory.NewAddon(), 100);

        Assert.Throws<DomainException>(() => position.Plus());
    }

    [Test]
    public void ProjectStatus_InvalidOrder_ShouldThrow()
    {
        Assert.Throws<DomainException>(() => new ProjectStatus("S", 0, true));
    }

    [Test]
    public void Question_Answer_ShouldSetAnswerDataAndRaiseEvent()
    {
        var question = DomainTestFactory.NewQuestion();
        var admin = DomainTestFactory.NewAdmin();

        question.AnswerQuestion(admin, "Test answer!");

        Assert.Multiple(() =>
        {
            Assert.That(question.Answer, Is.EqualTo("Test answer!"));
            Assert.That(question.AnsweredBy, Is.EqualTo(admin));
            Assert.That(question.AnsweredAt, Is.Not.Null);
            Assert.That(question.DomainEvents.OfType<QuestionAnsweredEvent>(), Has.Exactly(1).Items);
        });
    }

    [Test]
    public void Question_AnswerByNonAdmin_ShouldThrow()
    {
        var question = DomainTestFactory.NewQuestion();

        Assert.Throws<DomainException>(() => question.AnswerQuestion(DomainTestFactory.NewCustomer(), "x"));
    }

    [Test]
    public void Question_NewQuestion_ShouldHaveNoAnswer()
    {
        var question = DomainTestFactory.NewQuestion();

        Assert.Multiple(() =>
        {
            Assert.That(question.Answer, Is.Null);
            Assert.That(question.AnsweredAt, Is.Null);
        });
    }

    [Test]
    public void Project_RemoveAddon_Happy_ShouldRemove()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);

        project.RemoveAddon(addon.Id, hasOrders: false);

        Assert.That(project.Addons, Is.Empty);
    }

    [Test]
    public void Project_RemoveAddon_HasOrders_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);

        var ex = Assert.Throws<DomainException>(() => project.RemoveAddon(addon.Id, hasOrders: true));
        Assert.That(ex.Message, Does.Contain("Нельзя удалить"));
        Assert.That(project.Addons, Has.Count.EqualTo(1));
    }

    [Test]
    public void Project_RemoveAddon_NotFound_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();

        Assert.Throws<DomainException>(() => project.RemoveAddon(Guid.NewGuid(), hasOrders: false));
    }

    [Test]
    public void Project_RemoveAddon_NotEditMode_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);
        project.FinishEdit();

        Assert.Throws<ProjectNotEditModeException>(() => project.RemoveAddon(addon.Id, hasOrders: false));
    }

    [Test]
    public void Project_UpdateAddonName_Happy_ShouldUpdate()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);

        project.UpdateAddonName(addon.Id, "Renamed");

        Assert.That(addon.Name, Is.EqualTo("Renamed"));
    }

    [Test]
    public void Project_UpdateAddonName_NotFound_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();

        Assert.Throws<DomainException>(() => project.UpdateAddonName(Guid.NewGuid(), "Renamed"));
    }

    [Test]
    public void Project_UpdateAddonDescription_Happy_ShouldUpdate()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);

        project.UpdateAddonDescription(addon.Id, "New desc");

        Assert.That(addon.Description, Is.EqualTo("New desc"));
    }

    [Test]
    public void Project_UpdateAddonPrice_Happy_ShouldUpdate()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);

        project.UpdateAddonPrice(addon.Id, 42m);

        Assert.That((decimal)addon.Price, Is.EqualTo(42m));
    }

    [Test]
    public void Project_UpdateAddon_NotEditMode_ShouldThrow()
    {
        var project = DomainTestFactory.NewProject();
        var addon = DomainTestFactory.NewAddon();
        project.AddAddon(addon);
        project.FinishEdit();

        Assert.Throws<ProjectNotEditModeException>(() => project.UpdateAddonName(addon.Id, "Renamed"));
    }
}
