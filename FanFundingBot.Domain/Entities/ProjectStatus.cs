namespace FanFundingBot.Domain.Entities;

using FanFundingBot.Domain.Exceptions;

public class ProjectStatus
{
    public int Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool CanEditOrders { get; private set; }

    private ProjectStatus()
    {
    }

    public ProjectStatus(string displayName, int order, bool canEditOrders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (order < 1)
        {
            throw new DomainException("Порядок должен быть неотрицательным");
        }

        DisplayName = displayName;
        Order = order;
        CanEditOrders = canEditOrders;
    }
}
