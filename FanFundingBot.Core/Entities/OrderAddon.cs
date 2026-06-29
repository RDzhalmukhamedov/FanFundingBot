namespace FanFundingBot.Core.Entities;

public class OrderAddon
{
    public Guid OrderId { get; set; }
    public Guid AddonId { get; set; }
    public int Quantity { get; set; } = 1;

    public Order Order { get; set; } = null!;
    public Addon Addon { get; set; } = null!;
}
