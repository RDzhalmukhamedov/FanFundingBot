using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FanFundingBot.Data.Repositories;

public class AddonRepository : IAddonRepository
{
    private readonly AppDbContext _context;

    public AddonRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Addon?> GetByIdAsync(Guid id)
    {
        return await _context.Addons.FindAsync(id);
    }

    public async Task<List<Addon>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.Addons
            .Where(a => a.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<List<Addon>> GetByProjectIdPagedAsync(Guid projectId, int page, int pageSize)
    {
        return await _context.Addons
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByProjectIdAsync(Guid projectId)
    {
        return await _context.Addons
            .Where(a => a.ProjectId == projectId)
            .CountAsync();
    }

    public async Task AddAsync(Addon addon)
    {
        await _context.Addons.AddAsync(addon);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Addon addon)
    {
        _context.Addons.Update(addon);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var addon = await _context.Addons.FindAsync(id);
        if (addon != null)
        {
            _context.Addons.Remove(addon);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasOrdersAsync(Guid id)
    {
        return await _context.OrderAddons.AnyAsync(oa => oa.AddonId == id);
    }
}
