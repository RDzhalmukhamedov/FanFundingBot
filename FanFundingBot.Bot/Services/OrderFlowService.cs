using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Extensions;
using FanFundingBot.Bot.Models;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Exceptions;
using FanFundingBot.Core.Interfaces;

namespace FanFundingBot.Bot.Services;

public delegate Task OwnerNotificationDelegate(string message);

public class OrderFlowService : IOrderFlowService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IAddonRepository _addonRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOrderExternalStorage _externalStorage;
    private readonly OwnerNotificationDelegate _notifyOwner;

    public OrderFlowService(
        IOrderRepository orderRepository,
        IProjectRepository projectRepository,
        IAddonRepository addonRepository,
        IUserRepository userRepository,
        IOrderExternalStorage externalStorage,
        OwnerNotificationDelegate notifyOwner)
    {
        _orderRepository = orderRepository;
        _projectRepository = projectRepository;
        _addonRepository = addonRepository;
        _userRepository = userRepository;
        _externalStorage = externalStorage;
        _notifyOwner = notifyOwner;
    }

    public string? ValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "Пожалуйста, введите номер телефона.";

        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (!cleaned.IsValidPhone())
            return "Пожалуйста, введите корректный номер телефона в формате +7XXXXXXXXXX или 8XXXXXXXXXX.";

        return null;
    }

    public string? ValidateAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Пожалуйста, введите адрес доставки.";

        if (!address.IsValidAddress())
            return "Адрес должен содержать не менее 5 символов, включая буквы и цифры.";

        return null;
    }

    public async Task<Order> CreateOrderAsync(
        long userId,
        Guid projectId,
        HashSet<Guid> addonIds,
        Dictionary<Guid, int> addonQuantities,
        string phone,
        DeliveryMethod deliveryMethod,
        string? address)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.Phone != phone)
        {
            user.Phone = phone;
            await _userRepository.UpdateAsync(user);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = projectId,
            Phone = phone,
            DeliveryMethod = deliveryMethod,
            Address = deliveryMethod == DeliveryMethod.Delivery ? address : null,
            Status = OrderStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var addonId in addonIds)
        {
            order.OrderAddons.Add(new OrderAddon
            {
                OrderId = order.Id,
                AddonId = addonId,
                Quantity = addonQuantities.TryGetValue(addonId, out var qty) ? qty : 1
            });
        }

        try
        {
            await _orderRepository.AddAsync(order);

            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                var projectAddons = await _addonRepository.GetByProjectIdAsync(projectId);
                foreach (var oa in order.OrderAddons)
                    oa.Addon = projectAddons.FirstOrDefault(a => a.Id == oa.AddonId);

                order.User = user;
                order.Project = project!;

                await _externalStorage.SaveOrderAsync(order);
            }
            catch
            {
                await _orderRepository.DeleteAsync(order.Id);
                throw;
            }
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"Ошибка создания заказа {order.Id}: {ex.Message}");

            throw new OrderSaveException("Не удалось сохранить заказ. Попробуйте позже.", ex);
        }

        return order;
    }

    public async Task UpdateOrderAsync(
        Guid orderId,
        Guid projectId,
        HashSet<Guid> addonIds,
        Dictionary<Guid, int> addonQuantities,
        string phone,
        DeliveryMethod deliveryMethod,
        string? address)
    {
        var order = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        var originalPhone = order.Phone;
        var originalDeliveryMethod = order.DeliveryMethod;
        var originalAddress = order.Address;
        var originalAddons = order.OrderAddons
            .Select(oa => new OrderAddon { AddonId = oa.AddonId, Quantity = oa.Quantity })
            .ToList();

        order.Phone = phone;
        order.DeliveryMethod = deliveryMethod;
        order.Address = deliveryMethod == DeliveryMethod.Delivery ? address : null;

        try
        {
            await _orderRepository.DeleteOrderAddonsAsync(orderId);

            order.OrderAddons.Clear();

            foreach (var addonId in addonIds)
            {
                order.OrderAddons.Add(new OrderAddon
                {
                    OrderId = order.Id,
                    AddonId = addonId,
                    Quantity = addonQuantities.TryGetValue(addonId, out var qty) ? qty : 1
                });
            }

            await _orderRepository.UpdateAsync(order);

            try
            {
                var user = await _userRepository.GetByIdAsync(order.UserId);
                var project = await _projectRepository.GetByIdAsync(order.ProjectId);

                var projectAddons = await _addonRepository.GetByProjectIdAsync(order.ProjectId);
                foreach (var oa in order.OrderAddons)
                    oa.Addon ??= projectAddons.FirstOrDefault(a => a.Id == oa.AddonId);

                order.User = user!;
                order.Project = project!;

                await _externalStorage.SaveOrderAsync(order);
            }
            catch
            {
                order.Phone = originalPhone;
                order.DeliveryMethod = originalDeliveryMethod;
                order.Address = originalAddress;

                await _orderRepository.DeleteOrderAddonsAsync(order.Id);

                order.OrderAddons.Clear();

                foreach (var oa in originalAddons)
                {
                    order.OrderAddons.Add(new OrderAddon
                    {
                        OrderId = order.Id,
                        AddonId = oa.AddonId,
                        Quantity = oa.Quantity
                    });
                }

                await _orderRepository.UpdateAsync(order);
                throw;
            }
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"Ошибка обновления заказа {orderId}: {ex.Message}");

            if (ex is not OrderSaveException)
                throw new OrderSaveException("Не удалось обновить заказ. Попробуйте позже.", ex);
            throw;
        }
    }

    public async Task DeleteOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (order == null)
            return;

        var originalUserId = order.UserId;
        var originalProjectId = order.ProjectId;
        var originalPhone = order.Phone;
        var originalDeliveryMethod = order.DeliveryMethod;
        var originalAddress = order.Address;
        var originalStatus = order.Status;
        var originalCreatedAt = order.CreatedAt;
        var originalAddons = order.OrderAddons
            .Select(oa => new OrderAddon { AddonId = oa.AddonId, Quantity = oa.Quantity })
            .ToList();

        try
        {
            await _orderRepository.DeleteAsync(orderId);

            try
            {
                await _externalStorage.DeleteOrderAsync(orderId);
            }
            catch
            {
                var restoredOrder = new Order
                {
                    Id = orderId,
                    UserId = originalUserId,
                    ProjectId = originalProjectId,
                    Phone = originalPhone,
                    DeliveryMethod = originalDeliveryMethod,
                    Address = originalAddress,
                    Status = originalStatus,
                    CreatedAt = originalCreatedAt
                };

                foreach (var oa in originalAddons)
                {
                    restoredOrder.OrderAddons.Add(new OrderAddon
                    {
                        OrderId = orderId,
                        AddonId = oa.AddonId,
                        Quantity = oa.Quantity
                    });
                }

                await _orderRepository.AddAsync(restoredOrder);
                throw;
            }
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"Ошибка удаления заказа {orderId}: {ex.Message}");

            if (ex is not OrderSaveException)
                throw new OrderSaveException("Не удалось удалить заказ.", ex);
            throw;
        }
    }

    public async Task<Project?> GetProjectWithAddonsAsync(Guid projectId)
    {
        return await _projectRepository.GetByIdWithAddonsAsync(projectId);
    }

    public async Task<List<Addon>> GetAddonsPagedAsync(Guid projectId, int page, int pageSize)
    {
        return await _addonRepository.GetByProjectIdPagedAsync(projectId, page, pageSize);
    }

    public async Task<int> GetAddonCountAsync(Guid projectId)
    {
        return await _addonRepository.CountByProjectIdAsync(projectId);
    }

    public async Task<Order?> GetLatestOrderAsync(long userId)
    {
        var orders = await _orderRepository.GetByUserAsync(userId);
        return orders.FirstOrDefault();
    }

    public async Task<Order?> GetLatestOrderWithAddonsAsync(long userId)
    {
        var orders = await _orderRepository.GetByUserWithAddonsAsync(userId);
        return orders.FirstOrDefault();
    }

    private async Task NotifyOwnerAsync(string message)
    {
        try
        {
            await _notifyOwner(message);
        }
        catch
        {
        }
    }
}
