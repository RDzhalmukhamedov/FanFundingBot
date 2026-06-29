using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public class Order
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public DeliveryMethod DeliveryMethod { get; set; }
    public string? Address { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ICollection<OrderAddon> OrderAddons { get; set; } = new List<OrderAddon>();
}
