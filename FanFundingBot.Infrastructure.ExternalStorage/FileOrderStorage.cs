using System.Text.Json;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Infrastructure.ExternalStorage;

public class FileOrderStorage : IOrderExternalStorage
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

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

    public async Task SaveOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var orders = await LoadOrdersAsync(cancellationToken);
            orders.RemoveAll(o => o.Id == order.Id);
            orders.Add(ToStored(order));
            await SaveOrdersAsync(orders, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var orders = await LoadOrdersAsync(cancellationToken);
            orders.RemoveAll(o => o.Id == orderId);
            await SaveOrdersAsync(orders, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
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

    private async Task<List<StoredOrder>> LoadOrdersAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<List<StoredOrder>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Битый/legacy-шейп (master сериализовал List<Order>) — не роняем Outbox,
            // начинаем с пустого файла (потеря legacy-данных допустима для fallback).
            return [];
        }
    }

    private async Task SaveOrdersAsync(List<StoredOrder> orders, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(orders, options), ct);
    }
}
