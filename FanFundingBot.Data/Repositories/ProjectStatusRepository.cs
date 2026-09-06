using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Data.Repositories;

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

    public async Task<ProjectStatus?> GetByIdAsync(int id)
    {
        return await _context.ProjectStatusEntries.FindAsync(id);
    }

    public async Task AddAsync(ProjectStatus entry)
    {
        await _context.ProjectStatusEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }
}
