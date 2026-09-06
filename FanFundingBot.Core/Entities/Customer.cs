using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public class Customer : User
{
    public override UserRole Role => UserRole.Regular;

    public Customer(string firstName, string lastName, string userName) : base(firstName, lastName, userName)
    {
    }

    public DraftOrder StartOrdering(Project project)
    {
        if (_orders.Any(order => order.Value.Project.Id == project.Id))
        {
            //throw new SeatBookedException(clientId);
            throw new Exception(project.Name);
        }

        var newOrder = new DraftOrder(this, project);
        _orders.Add(newOrder.Id, newOrder);

        return newOrder;
    }

    public DraftOrder EditOrder(Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var orderToEdit))
        {
            //throw new SeatBookedException(clientId);
            throw new Exception(orderId.ToString());
        }
        var editableOrder = orderToEdit.Edit();
        _orders[orderId] = editableOrder;

        return editableOrder;
    }

    public FormedOrder FormOrder(Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var draftOrder))
        {
            //throw new SeatBookedException(clientId);
            throw new Exception(orderId.ToString());
        }
        var formedOrder = draftOrder.Form();
        _orders[orderId] = formedOrder;

        return formedOrder;
    }

    public void DeleteOrder(Guid orderId)
    {
        if (!_orders.ContainsKey(orderId))
        {
            //throw new SeatBookedException(clientId);
            throw new Exception(orderId.ToString());
        }
        
        _orders.Remove(orderId);
    }

}
