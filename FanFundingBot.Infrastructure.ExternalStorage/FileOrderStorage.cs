using System.Text.Json;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;

namespace FanFundingBot.Infrastructure.ExternalStorage;

public class FileOrderStorage : IOrderExternalStorage
{
    private readonly string _filePath;

    private sealed record StoredPosition(Guid AddonId, string AddonName, decimal Price, int Quantity);
    private sealed record StoredOrder(
        Guid Id,
        long UserId,
        Guid ProjectId,
        string Phone,
        string DeliveryMethod,
        string? Address,
        string Status,
        DateTime CreatedAt,
        List<StoredPosition> Positions);

    public FileOrderStorage(string? directoryPath = null)
    {
        var dir = directoryPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orders");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "orders.json");
    }

    public async Task SaveOrderAsync(Order order)
    {
        var orders = await LoadOrdersAsync();
        orders.RemoveAll(o => o.Id == order.Id);
        orders.Add(ToStored(order));
        await SaveOrdersAsync(orders);
    }

    public async Task DeleteOrderAsync(Guid orderId)
    {
        var orders = await LoadOrdersAsync();
        orders.RemoveAll(o => o.Id == orderId);
        await SaveOrdersAsync(orders);
    }

    private static StoredOrder ToStored(Order order)
    {
        return new StoredOrder(
            order.Id,
            order.User.Id,
            order.Project.Id,
            order.Phone,
            order.DeliveryMethod.ToString(),
            order.Address,
            order.Status.ToString(),
            order.CreatedAt,
            order.OrderAddons
                .Select(p => new StoredPosition(p.Addon.Id, p.Addon.Name, p.Addon.Price, p.Quantity))
                .ToList());
    }

    private async Task<List<StoredOrder>> LoadOrdersAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<StoredOrder>>(json) ?? [];
    }

    private async Task SaveOrdersAsync(List<StoredOrder> orders)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(orders, options));
    }
}
