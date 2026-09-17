using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence.Repositories;

public class ProjectStatusRepository : IProjectStatusRepository
{
    private readonly AppDbContext _context;

    public ProjectStatusRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProjectStatus>> GetAllAsync()
    {
        return await _context.ProjectStatusEntries
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<int> GetMaxOrderAsync()
    {
        return await _context.ProjectStatusEntries.MaxAsync(s => (int?)s.Order) ?? 0;
    }

    public async Task<ProjectStatus?> GetByIdAsync(int id)
    {
        return await _context.ProjectStatusEntries.FindAsync(id);
    }

    public async Task AddAsync(ProjectStatus entry)
    {
        await _context.ProjectStatusEntries.AddAsync(entry);
    }

    public async Task<ProjectStatus> AddWithNextOrderAsync(
        string displayName, bool canEditOrders, CancellationToken cancellationToken = default)
    {
        // Retry×3: Max+1 гонка закрыта unique(Order) + повтор. SaveChanges сразу
        // (исключение из контракта «репозитории не коммитят» — см. интерфейс):
        // retry поверх отложенного UoW-коммита невозможен.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var order = await GetMaxOrderAsync() + 1;
            var entry = new ProjectStatus(displayName, order, canEditOrders);
            await _context.ProjectStatusEntries.AddAsync(entry, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return entry;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _context.Entry(entry).State = EntityState.Detached;
            }
        }

        throw new ProjectStatusOrderConflictException();
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // PG unique_violation (23505); без зависимости от Npgsql — по сообщению.
        for (var e = (Exception?)ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("23505")
                || e.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
