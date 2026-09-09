using FanFundingBot.Application.Orders;
using FanFundingBot.Application.Projects.DTOs;
using FluentAssertions;

namespace FanFundingBot.Tests.Application.Orders;

[TestFixture]
public class OrderPreviewTests
{
    private static ProjectDto NewProject(params AddonDto[] addons) =>
        new(Guid.NewGuid(), "P", null, 1, "Draft", true, null, addons.ToList(), new List<ProjectMemberDto>());

    [Test]
    public void Build_EmptySelection_ShouldReturnZeroTotal()
    {
        var project = NewProject(new AddonDto(Guid.NewGuid(), "A", null, 100m));

        var (lines, total) = OrderPreview.Build(project, Array.Empty<Guid>(), new Dictionary<Guid, int>());

        lines.Should().BeEmpty();
        total.Should().Be(0m);
    }

    [Test]
    public void Build_MissingQuantity_ShouldDefaultToOne()
    {
        var addonId = Guid.NewGuid();
        var project = NewProject(new AddonDto(addonId, "A", null, 100m));

        var (lines, total) = OrderPreview.Build(
            project, new[] { addonId }, new Dictionary<Guid, int>());

        lines.Should().ContainSingle().Which.Quantity.Should().Be(1);
        total.Should().Be(100m);
    }

    [Test]
    public void Build_UnknownAddon_ShouldBeSkipped()
    {
        var project = NewProject(new AddonDto(Guid.NewGuid(), "A", null, 100m));

        var (lines, total) = OrderPreview.Build(
            project,
            new[] { Guid.NewGuid() },
            new Dictionary<Guid, int>());

        lines.Should().BeEmpty();
        total.Should().Be(0m);
    }

    [Test]
    public void Build_MultiplePositions_ShouldSumPriceTimesQuantity()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var project = NewProject(
            new AddonDto(id1, "A1", null, 100m),
            new AddonDto(id2, "A2", null, 50m));

        var (lines, total) = OrderPreview.Build(
            project,
            new[] { id1, id2 },
            new Dictionary<Guid, int> { [id1] = 2, [id2] = 3 });

        lines.Should().HaveCount(2);
        lines.First(l => l.AddonId == id1).Total.Should().Be(200m);
        total.Should().Be(350m);
    }
}
