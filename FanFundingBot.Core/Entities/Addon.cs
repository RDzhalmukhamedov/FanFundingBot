namespace FanFundingBot.Core.Entities;

public class Addon
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<OrderAddon> OrderAddons { get; set; } = new List<OrderAddon>();
}
