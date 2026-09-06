namespace FanFundingBot.Core.Entities;

public class ProjectStatus
{
    public int Id { get; }
    public string DisplayName { get; }
    public int Order { get; }
    public bool CanEditOrders { get; }

    public ProjectStatus(int id, string name, int order, bool canEditOrders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (id < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Идентификатор должен быть неотрицательным"); // TODO Возможно сделать Extension
        }
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "Порядок должен быть неотрицательным"); // TODO Возможно сделать Extension
        }
        
        Id = id;
        DisplayName = name;
        Order = order;
        CanEditOrders = canEditOrders;
    }
}
