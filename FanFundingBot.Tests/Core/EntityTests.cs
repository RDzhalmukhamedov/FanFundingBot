using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;

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
    public void Addon_SetPrice_Negative_ShouldThrow()
    {
        var addon = new Addon();

        Assert.Throws<ArgumentOutOfRangeException>(() => addon.SetPrice(-1));
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
    public void User_Customer_ShouldHaveRegularRole()
    {
        var user = new Customer(12345, "John", "Doe", "johndoe");

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
    public void User_Admin_ShouldHaveAdminRole()
    {
        var admin = new Admin(1, "A", "B", "admin");

        Assert.Multiple(() =>
        {
            Assert.That(admin.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(admin.IsAdmin(), Is.True);
        });
    }

    [Test]
    public void User_PromoteToAdmin_ShouldChangeRole()
    {
        var user = new Customer(1, "John", "Doe", "johndoe");
        user.PromoteToAdmin();

        Assert.That(user.Role, Is.EqualTo(UserRole.Admin));
    }

    [Test]
    public void User_SetPhone_ShouldUpdate()
    {
        var user = new Customer(1, "John", "Doe", "johndoe");
        user.SetPhone("+71234567890");

        Assert.That(user.Phone, Is.EqualTo("+71234567890"));
    }

    [Test]
    public void Customer_StartOrdering_DuplicateProject_ShouldThrow()
    {
        var customer = DomainTestFactory.NewCustomer();
        var project = DomainTestFactory.NewProject();

        customer.StartOrdering(project);

        Assert.Throws<DomainException>(() => customer.StartOrdering(project));
    }

    [Test]
    public void Customer_FormAndEditOrder_ShouldChangeState()
    {
        var customer = DomainTestFactory.NewCustomer();
        var project = DomainTestFactory.NewProject();

        var draft = customer.StartOrdering(project);
        var formed = customer.FormOrder(draft.Id);

        Assert.That(formed, Is.InstanceOf<FormedOrder>());

        var edited = customer.EditOrder(draft.Id);

        Assert.That(edited, Is.InstanceOf<DraftOrder>());
    }

    [Test]
    public void DraftOrder_Form_ShouldCarryState()
    {
        var addonId = Guid.NewGuid();
        var draft = DomainTestFactory.NewDraft(addonId: addonId, quantity: 2);

        var formed = draft.Form();

        Assert.Multiple(() =>
        {
            Assert.That(formed.Id, Is.EqualTo(draft.Id));
            Assert.That(formed.Phone, Is.EqualTo(draft.Phone));
            Assert.That(formed.OrderAddons, Has.Count.EqualTo(1));
            Assert.That(formed.OrderAddons[0].Addon.Id, Is.EqualTo(addonId));
            Assert.That(formed.OrderAddons[0].Quantity, Is.EqualTo(2));
        });
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

        Assert.Throws<ArgumentOutOfRangeException>(() => draft.DecrementAddonQuantity(addonId));
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
            Assert.That(draft.Phone, Is.EqualTo("+79998887766"));
            Assert.That(draft.OrderAddons, Has.Count.EqualTo(formed.OrderAddons.Count));
        });
    }

    [Test]
    public void OrderPosition_InvalidQuantity_ShouldThrow()
    {
        var addon = DomainTestFactory.NewAddon();

        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderPosition(addon, 0));
    }

    [Test]
    public void OrderPosition_PlusOverLimit_ShouldThrow()
    {
        var position = new OrderPosition(DomainTestFactory.NewAddon(), 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => position.Plus());
    }

    [Test]
    public void ProjectStatus_InvalidOrder_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectStatus("S", 0, true));
    }

    [Test]
    public void Question_Answer_ShouldSetAnswerData()
    {
        var question = DomainTestFactory.NewQuestion();
        var admin = DomainTestFactory.NewAdmin();

        question.AnswerQuestion(admin, "Test answer!");

        Assert.Multiple(() =>
        {
            Assert.That(question.Answer, Is.EqualTo("Test answer!"));
            Assert.That(question.AnsweredBy, Is.EqualTo(admin));
            Assert.That(question.AnsweredAt, Is.Not.Null);
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
}
