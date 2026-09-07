using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Tests.Bot.Keyboards;

[TestFixture]
public class KeyboardTests
{
    [Test]
    public void MainMenuKeyboard_ForRegularUser_ShouldHaveBasicButtons()
    {
        var keyboard = MainMenuKeyboard.Create(false);

        var buttons = keyboard.Keyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.Text == "📋 Статус проекта"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "📦 Мой заказ"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "🛒 Оформить заказ"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "ℹ️ О проекте"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "❓ Задать вопрос"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "🔄 Обновить статус"), Is.False);
            Assert.That(buttons.Any(b => b.Text == "📢 Рассылка"), Is.False);
        });
    }

    [Test]
    public void MainMenuKeyboard_ForAdmin_ShouldHaveAdminButtons()
    {
        var keyboard = MainMenuKeyboard.Create(true);

        var buttons = keyboard.Keyboard!.SelectMany(row => row).Select(b => b.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons, Does.Contain("🔄 Обновить статус"));
            Assert.That(buttons, Does.Contain("📢 Рассылка"));
            Assert.That(buttons, Does.Contain("✉️ Ответить на вопрос"));
        });
    }

    [Test]
    public void DeliveryMethodKeyboard_ShouldHavePickupAndDelivery()
    {
        var keyboard = DeliveryMethodKeyboard.Create();

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.Text == "🚶 Самовывоз"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "🚚 Доставка"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "delivery:pickup"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "delivery:delivery"), Is.True);
        });
    }

    [Test]
    public void ConfirmationKeyboard_ShouldHaveConfirmAndEdit()
    {
        var keyboard = ConfirmationKeyboard.Create();

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.CallbackData == "order_confirm"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "order_edit"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "order_cancel"), Is.True);
        });
    }

    [Test]
    public void ConfirmationKeyboard_WithAddons_ShouldHaveQuantityButtons()
    {
        var addons = new List<(Guid Id, string Name, decimal Price, int Quantity)>
        {
            (Guid.NewGuid(), "Addon1", 100m, 2),
            (Guid.NewGuid(), "Addon2", 200m, 1)
        };

        var keyboard = ConfirmationKeyboard.Create(addons);

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.CallbackData == "order_confirm"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "order_edit"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "order_cancel"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData!.StartsWith("addon_qty_inc:")), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData!.StartsWith("addon_qty_dec:")), Is.True);
            Assert.That(buttons.Any(b => b.Text.Contains("2x Addon1")), Is.True);
            Assert.That(buttons.Any(b => b.Text.Contains("1x Addon2")), Is.True);
        });
    }

    [Test]
    public void ConfirmationKeyboard_WithSingleQuantity_ShouldStillHaveDecButton()
    {
        var addons = new List<(Guid Id, string Name, decimal Price, int Quantity)>
        {
            (Guid.NewGuid(), "A1", 50m, 1)
        };

        var keyboard = ConfirmationKeyboard.Create(addons);

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.That(buttons.Any(b => b.CallbackData!.StartsWith("addon_qty_dec:")), Is.True);
    }

    [Test]
    public void BackKeyboard_ShouldHaveBackButton()
    {
        var keyboard = BackKeyboard.Create();

        var buttons = keyboard.Keyboard!.SelectMany(row => row).ToList();
        Assert.That(buttons.Any(b => b.Text == "◀️ Назад"), Is.True);
    }

    [Test]
    public void BackKeyboard_CustomText_ShouldUseCustomText()
    {
        var keyboard = BackKeyboard.Create("🔙 Назад");

        var buttons = keyboard.Keyboard!.SelectMany(row => row).ToList();
        Assert.That(buttons.Any(b => b.Text == "🔙 Назад"), Is.True);
    }

    [Test]
    public void AddonSelectionKeyboard_ShouldShowAddonsWithCheckmarks()
    {
        var addons = new List<Addon>
        {
            new Addon(Guid.NewGuid(), "Addon1", "Desc", 100m, EditMode.Formed),
            new Addon(Guid.NewGuid(), "Addon2", "Desc", 200m, EditMode.Formed)
        };
        var selectedIds = new HashSet<Guid> { addons[0].Id };

        var (keyboard, totalPages) = AddonSelectionKeyboard.Create(addons, selectedIds, 1, 2);

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.Text.Contains("✅") && b.Text.Contains("Addon1")), Is.True);
            Assert.That(buttons.Any(b => !b.Text.Contains("✅") && b.Text.Contains("Addon2")), Is.True);
            Assert.That(totalPages, Is.EqualTo(1));
        });
    }

    [Test]
    public void AddonSelectionKeyboard_WithManyAddons_ShouldShowPagination()
    {
        var addons = Enumerable.Range(0, 6).Select(i => new Addon(
            Guid.NewGuid(),
            $"Addon{i}",
            "Desc",
            i * 10,
            EditMode.Formed)).ToList();

        var (_, totalPages) = AddonSelectionKeyboard.Create(addons, new HashSet<Guid>(), 1, 12);

        Assert.That(totalPages, Is.EqualTo(2));
    }

    [Test]
    public void BroadcastConfirmationKeyboard_ShouldHaveSendAndCancel()
    {
        var keyboard = ConfirmationKeyboard.CreateBroadcastConfirmation();

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.CallbackData == "admin_broadcast_confirm"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "admin_cancel"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "✅ Отправить"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "❌ Отмена"), Is.True);
        });
    }

    [Test]
    public void StatusConfirmationKeyboard_ShouldHaveConfirmAndCancel()
    {
        var projectId = Guid.NewGuid();
        const int statusId = 5;
        var keyboard = ConfirmationKeyboard.CreateStatusConfirmation(projectId, statusId);

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.CallbackData == $"admin_status_confirm:{projectId}:{statusId}"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == $"admin_status_cancel:{projectId}"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "✅ Подтвердить"), Is.True);
            Assert.That(buttons.Any(b => b.Text == "❌ Отмена"), Is.True);
        });
    }

    [Test]
    public void AddonSelectionKeyboard_ShouldHaveConfirmAndCancel()
    {
        var addons = new List<Addon>
        {
            new Addon(Guid.NewGuid(), "A1", "Desc", 50m, EditMode.Formed)
        };

        var (keyboard, _) = AddonSelectionKeyboard.Create(addons, new HashSet<Guid>(), 1, 1);

        var buttons = keyboard.InlineKeyboard!.SelectMany(row => row).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(buttons.Any(b => b.CallbackData == "addon_confirm"), Is.True);
            Assert.That(buttons.Any(b => b.CallbackData == "order_cancel"), Is.True);
        });
    }
}
