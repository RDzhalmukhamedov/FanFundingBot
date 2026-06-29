using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public class ProjectStatusEntry
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool CanEdit { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
