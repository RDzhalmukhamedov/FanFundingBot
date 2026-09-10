using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Bot.Models;

public class UserSession
{
    public OrderFlowState State { get; set; } = OrderFlowState.Idle;
    public Guid? SelectedProjectId { get; set; }
    public HashSet<Guid> SelectedAddonIds { get; set; } = new();
    public int AddonPage { get; set; } = 1;
    public string? Phone { get; set; }
    public DeliveryMethod? DeliveryMethod { get; set; }
    public string? Address { get; set; }
    public int? EditMessageId { get; set; }
    public string? PendingAction { get; set; }
    public Guid? PendingQuestionId { get; set; }

    // Order editing
    public Guid? EditingOrderId { get; set; }

    // Addon quantities on confirmation
    public Dictionary<Guid, int> AddonQuantities { get; set; } = new();

    // Admin: temp project creation
    public string? TempProjectName { get; set; }
    public string? TempProjectDescription { get; set; }
    public List<(string Name, string Description, decimal Price)>? TempAddons { get; set; }
    public Guid? TempProjectId { get; set; }
    public string? TempAddonName { get; set; }
    public string? TempAddonDescription { get; set; }
    public Guid? TempAddonId { get; set; }

    // Admin: temp status creation
    public string? TempStatusName { get; set; }
    public bool? TempStatusCanEdit { get; set; }

    // Admin: temp broadcast
    public string? TempBroadcastText { get; set; }
}
