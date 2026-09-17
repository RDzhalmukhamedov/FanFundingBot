using FanFundingBot.Application.Orders;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Domain.Entities;
using FluentAssertions;

namespace FanFundingBot.Tests.Core;

[TestFixture]
public class OrderTotalTests
{
    [Test]
    public void EmptyOrder_TotalShouldBeZero()
    {
        var order = new DraftOrder(DomainTestFactory.NewCustomer(), DomainTestFactory.NewProject());

        ((decimal)order.Total).Should().Be(0m);
    }

    [Test]
    public void Total_ShouldSumPriceTimesQuantity()
    {
        var project = DomainTestFactory.NewProject();
        var order = new DraftOrder(DomainTestFactory.NewCustomer(), project);
        var addon1 = DomainTestFactory.NewAddon(price: 100m);
        var addon2 = DomainTestFactory.NewAddon(price: 49.5m);
        order.AddAddon(addon1, 2);
        order.AddAddon(addon2, 3);

        ((decimal)order.Total).Should().Be(100m * 2 + 49.5m * 3);
    }

    [Test]
    public void CalculateTotal_ShouldMatchOrderPreview()
    {
        var project = DomainTestFactory.NewProject();
        var addon1 = DomainTestFactory.NewAddon(price: 100m);
        var addon2 = DomainTestFactory.NewAddon(price: 25m);
        project.StartEdit();
        project.AddAddon(addon1);
        project.AddAddon(addon2);
        project.FinishEdit();

        var dto = new ProjectDto(
            project.Id, project.Name, project.Description ?? "",
            0, "", true, null,
            new List<AddonDto>
            {
                new(addon1.Id, addon1.Name, null, 100m),
                new(addon2.Id, addon2.Name, null, 25m),
            },
            new List<ProjectMemberDto>());
        var quantities = new Dictionary<Guid, int> { [addon1.Id] = 2, [addon2.Id] = 1 };

        var (_, previewTotal) = OrderPreview.Build(dto, new[] { addon1.Id, addon2.Id }, quantities);
        var domainTotal = (decimal)Order.CalculateTotal(new[] { (100m, 2), (25m, 1) });

        previewTotal.Should().Be(domainTotal);
        previewTotal.Should().Be(225m);
    }
}
