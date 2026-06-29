using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Data.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        return await _context.Orders.FindAsync(id);
    }

    public async Task<Order?> GetByIdWithAddonsAsync(Guid id)
    {
        return await _context.Orders
            .Include(o => o.OrderAddons)
            .ThenInclude(oa => oa.Addon)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Order>> GetByUserAsync(long userId)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByUserWithAddonsAsync(long userId)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderAddons)
            .ThenInclude(oa => oa.Addon)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByProjectAsync(Guid projectId)
    {
        return await _context.Orders
            .Where(o => o.ProjectId == projectId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByProjectAndUserAsync(Guid projectId, long userId)
    {
        return await _context.Orders
            .Where(o => o.ProjectId == projectId && o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order != null)
        {
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteOrderAddonsAsync(Guid orderId)
    {
        var addons = await _context.OrderAddons
            .Where(oa => oa.OrderId == orderId)
            .ToListAsync();
        _context.OrderAddons.RemoveRange(addons);
        await _context.SaveChangesAsync();
    }
}
