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
            .Include(o => o.User)
            .Include(o => o.Project)
            .Include(o => o.OrderAddons)
            .ThenInclude(p => p.Addon)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Order>> GetByUserAsync(long userId)
    {
        return await _context.Orders
            .Where(o => o.User.Id == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByUserWithAddonsAsync(long userId)
    {
        return await _context.Orders
            .Where(o => o.User.Id == userId)
            .Include(o => o.User)
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
            .Include(o => o.User)
            .Include(o => o.Project)
            .Include(o => o.OrderAddons)
            .ThenInclude(p => p.Addon)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<Order> Items, int TotalCount)> GetByProjectPagedAsync(Guid projectId, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Orders.Where(o => o.Project.Id == projectId);
        var total = await query.CountAsync();

        var items = await query
            .Include(o => o.User)
            .Include(o => o.Project)
            .Include(o => o.OrderAddons)
            .ThenInclude(p => p.Addon)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<Order>> GetByProjectAndUserAsync(Guid projectId, long userId)
    {
        return await _context.Orders
            .Where(o => o.Project.Id == projectId && o.User.Id == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
    }

    public async Task UpdateAsync(Order order)
    {
        // Позиции всегда перезаписываются целиком: удаляем строки и вставляем заново.
        // Сам заказ — Attach + точечные скаляры: Update() всего графа метил бы Modified
        // связанные User/Project/Addon и перезаписал бы справочники полями из навигаций.
        var tracked = _context.ChangeTracker.Entries<Order>()
            .FirstOrDefault(e => e.Entity.Id == order.Id);
        if (tracked is not null)
        {
            // [same-instance update]: aggregate already tracked and mutated in place
            // (e.g. status change). Positions here ARE the DB rows: detach +
            // Deleted->Added would cancel the delete and re-insert the same PKs
            // (23505 PK_OrderPositions). Leave the graph as is; SaveChanges
            // will persist the mutated scalars.
            if (ReferenceEquals(tracked.Entity, order))
                return;
            tracked.State = EntityState.Detached;
        }

        var existing = await _context.OrderPositions
            .Where(p => p.OrderId == order.Id)
            .ToListAsync();
        _context.OrderPositions.RemoveRange(existing);

        _context.Orders.Attach(order);
        _context.Entry(order).Property(o => o.Phone).IsModified = true;
        _context.Entry(order).Property(o => o.DeliveryMethod).IsModified = true;
        _context.Entry(order).Property(o => o.Address).IsModified = true;
        _context.Entry(order).Property(o => o.Status).IsModified = true;
        foreach (var position in order.OrderAddons)
            _context.Entry(position).State = EntityState.Added;
    }

    public async Task DeleteAsync(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order != null)
        {
            _context.Orders.Remove(order);
        }
    }
}
