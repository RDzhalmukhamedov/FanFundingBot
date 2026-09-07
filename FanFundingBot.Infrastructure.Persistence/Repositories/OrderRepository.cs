using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence.Repositories;

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
            .Include(o => o.Customer)
            .Include(o => o.Project)
            .Include(o => o.OrderAddons)
            .ThenInclude(p => p.Addon)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Order>> GetByUserAsync(long userId)
    {
        return await _context.Orders
            .Where(o => o.Customer.Id == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByUserWithAddonsAsync(long userId)
    {
        return await _context.Orders
            .Where(o => o.Customer.Id == userId)
            .Include(o => o.Customer)
            .Include(o => o.Project)
            .Include(o => o.OrderAddons)
            .ThenInclude(p => p.Addon)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByProjectAsync(Guid projectId)
    {
        return await _context.Orders
            .Where(o => o.Project.Id == projectId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByProjectAndUserAsync(Guid projectId, long userId)
    {
        return await _context.Orders
            .Where(o => o.Project.Id == projectId && o.Customer.Id == userId)
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
        // Позиции всегда перезаписываются целиком: удаляем строки и вставляем заново.
        // (В Фазе 2 будет UnitOfWork с корректным трекингом агрегата.)
        var tracked = _context.ChangeTracker.Entries<Order>()
            .FirstOrDefault(e => e.Entity.Id == order.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        var existing = await _context.OrderPositions
            .Where(p => p.OrderId == order.Id)
            .ToListAsync();
        _context.OrderPositions.RemoveRange(existing);

        _context.Orders.Update(order);
        foreach (var position in order.OrderAddons)
            _context.Entry(position).State = EntityState.Added;

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
}
