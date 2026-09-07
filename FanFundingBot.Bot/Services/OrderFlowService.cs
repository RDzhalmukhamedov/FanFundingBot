using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Extensions;
using FanFundingBot.Bot.Models;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Interfaces;

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
        FormedOrder? formed = null;
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException("User not found");

            var duplicates = await _orderRepository.GetByProjectAndUserAsync(projectId, userId);
            if (duplicates.Any(o => o.Status != OrderStatus.Cancelled))
                throw new InvalidOperationException("Order already exists");

            if (user.Phone != phone)
            {
                user.SetPhone(phone);
                await _userRepository.UpdateAsync(user);
            }

            var project = await _projectRepository.GetByIdWithAddonsAsync(projectId);
            if (project == null)
                throw new InvalidOperationException("Project not found");

            var draft = new DraftOrder(user, project);

            foreach (var addonId in addonIds)
            {
                var addon = project.Addons.FirstOrDefault(a => a.Id == addonId);
                if (addon == null)
                    throw new InvalidOperationException($"Addon {addonId} not found");

                draft.AddAddon(addon, addonQuantities.TryGetValue(addonId, out var qty) ? qty : 1);
            }

            draft.SetPhoneNumber(phone);
            draft.SetDeliveryMethod(deliveryMethod);
            if (deliveryMethod == DeliveryMethod.Delivery)
                draft.SetShippingAddress(address);

            formed = draft.Form();

            await _orderRepository.AddAsync(formed);

            try
            {
                await _externalStorage.SaveOrderAsync(formed);
            }
            catch
            {
                await _orderRepository.DeleteAsync(formed.Id);
                throw;
            }
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"Ошибка создания заказа {formed?.Id}: {ex.Message}");

            throw new OrderSaveException("Не удалось сохранить заказ. Попробуйте позже.", ex);
        }

        return formed;
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
        var original = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (original == null)
            throw new InvalidOperationException("Order not found");

        var snapshot = Snapshot(original);

        try
        {
            var project = await _projectRepository.GetByIdWithAddonsAsync(original.Project.Id);
            if (project == null)
                throw new InvalidOperationException("Project not found");

            var updated = ApplyState(original, project.Addons, addonIds, addonQuantities, phone, deliveryMethod, address);
            await _orderRepository.UpdateAsync(updated);

            try
            {
                var saved = await _orderRepository.GetByIdWithAddonsAsync(orderId);
                await _externalStorage.SaveOrderAsync(saved!);
            }
            catch
            {
                var current = await _orderRepository.GetByIdWithAddonsAsync(orderId);
                var currentProject = await _projectRepository.GetByIdWithAddonsAsync(current!.Project.Id);
                var restored = ApplySnapshot(current, currentProject!.Addons, snapshot);
                await _orderRepository.UpdateAsync(restored);
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

        try
        {
            await _orderRepository.DeleteAsync(orderId);

            try
            {
                await _externalStorage.DeleteOrderAsync(orderId);
            }
            catch
            {
                await _orderRepository.AddAsync(order);
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

    private static OrderSnapshot Snapshot(Order order)
    {
        return new OrderSnapshot(
            order.Phone,
            order.DeliveryMethod,
            order.Address,
            order.OrderAddons.Select(p => (p.Addon.Id, (int)p.Quantity)).ToList());
    }

    private FormedOrder ApplyState(
        Order order,
        IReadOnlyList<Addon> projectAddons,
        HashSet<Guid> addonIds,
        Dictionary<Guid, int> addonQuantities,
        string phone,
        DeliveryMethod deliveryMethod,
        string? address)
    {
        DraftOrder draft = order is DraftOrder d ? d : order.Edit();

        foreach (var position in draft.OrderAddons.ToList())
            draft.DeleteAddon(position.Addon);

        var addonsById = projectAddons.ToDictionary(a => a.Id);
        foreach (var addonId in addonIds)
        {
            if (!addonsById.TryGetValue(addonId, out var addon))
                throw new InvalidOperationException($"Addon {addonId} not found");

            draft.AddAddon(addon, addonQuantities.TryGetValue(addonId, out var qty) ? qty : 1);
        }

        draft.SetPhoneNumber(phone);
        draft.SetDeliveryMethod(deliveryMethod);
        draft.SetShippingAddress(deliveryMethod == DeliveryMethod.Delivery ? address : null);

        return draft.Form();
    }

    private FormedOrder ApplySnapshot(Order order, IReadOnlyList<Addon> projectAddons, OrderSnapshot snapshot)
    {
        return ApplyState(
            order,
            projectAddons,
            snapshot.Positions.Select(p => p.AddonId).ToHashSet(),
            snapshot.Positions.ToDictionary(p => p.AddonId, p => p.Quantity),
            snapshot.Phone,
            snapshot.DeliveryMethod,
            snapshot.Address);
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

    private sealed record OrderSnapshot(
        string Phone,
        DeliveryMethod DeliveryMethod,
        string? Address,
        List<(Guid AddonId, int Quantity)> Positions);
}
