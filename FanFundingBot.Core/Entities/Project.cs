using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StatusId { get; set; }
    public ProjectStatusEntry Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ICollection<Addon> Addons { get; set; } = new List<Addon>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
