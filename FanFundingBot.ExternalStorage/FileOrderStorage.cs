using System.Text.Json;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Interfaces;

namespace FanFundingBot.ExternalStorage;

public class FileOrderStorage : IOrderExternalStorage
{
    private readonly string _filePath;

    public FileOrderStorage(string? directoryPath = null)
    {
        var dir = directoryPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orders");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "orders.json");
    }

    public async Task SaveOrderAsync(Order order)
    {
        var orders = await LoadOrdersAsync();
        orders.Add(order);
        await SaveOrdersAsync(orders);
    }

    public async Task DeleteOrderAsync(Guid orderId)
    {
        var orders = await LoadOrdersAsync();
        orders.RemoveAll(o => o.Id == orderId);
        await SaveOrdersAsync(orders);
    }

    private async Task<List<Order>> LoadOrdersAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<Order>>(json) ?? [];
    }

    private async Task SaveOrdersAsync(List<Order> orders)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(orders, options));
    }
}