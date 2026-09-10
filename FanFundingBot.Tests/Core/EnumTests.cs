using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Tests.Core;

[TestFixture]
public class EnumTests
{
    [Test]
    public void UserRole_ShouldHaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)UserRole.Regular, Is.EqualTo(0));
            Assert.That((int)UserRole.Admin, Is.EqualTo(1));
        });
    }

    [Test]
    public void OrderStatus_ShouldHaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)OrderStatus.New, Is.EqualTo(0));
            Assert.That((int)OrderStatus.Confirmed, Is.EqualTo(1));
            Assert.That((int)OrderStatus.Completed, Is.EqualTo(2));
            Assert.That((int)OrderStatus.Cancelled, Is.EqualTo(3));
        });
    }

    [Test]
    public void DeliveryMethod_ShouldHaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)DeliveryMethod.Pickup, Is.EqualTo(0));
            Assert.That((int)DeliveryMethod.Delivery, Is.EqualTo(1));
        });
    }

    [Test]
    public void OrderFlowState_ShouldHaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)OrderFlowState.Idle, Is.EqualTo(0));
            Assert.That((int)OrderFlowState.SelectingAddons, Is.EqualTo(1));
            Assert.That((int)OrderFlowState.EnteringPhone, Is.EqualTo(2));
            Assert.That((int)OrderFlowState.SelectingDelivery, Is.EqualTo(3));
            Assert.That((int)OrderFlowState.EnteringAddress, Is.EqualTo(4));
            Assert.That((int)OrderFlowState.Confirming, Is.EqualTo(5));
        });
    }

    [Test]
    public void UserRole_Admin_ShouldBeDefined()
    {
        Assert.That(Enum.IsDefined(typeof(UserRole), "Admin"), Is.True);
    }
}
