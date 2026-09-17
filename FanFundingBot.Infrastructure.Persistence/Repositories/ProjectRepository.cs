using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Status)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetByIdWithAddonsAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Addons)
            .Include(p => p.Status)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Project>> GetAllAsync()
    {
        return await _context.Projects
            .Include(p => p.Status)
            .Include(p => p.Members)
            .ToListAsync();
    }

    public async Task<List<Project>> GetAllWithAddonsAsync()
    {
        return await _context.Projects
            .Include(p => p.Addons)
            .Include(p => p.Status)
            .Include(p => p.Members)
            .ToListAsync();
    }

    public async Task AddAsync(Project project)
    {
        await _context.Projects.AddAsync(project);
    }

    public async Task UpdateAsync(Project project)
    {
        var tracked = _context.ChangeTracker.Entries<Project>()
            .FirstOrDefault(e => e.Entity.Id == project.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        var existingAddonIds = await _context.Addons
            .Where(a => EF.Property<Guid>(a, "ProjectId") == project.Id)
            .Select(a => a.Id)
            .ToListAsync();

        var currentAddonIds = project.Addons.Select(a => a.Id).ToHashSet();
        var removedAddonIds = existingAddonIds.Where(id => !currentAddonIds.Contains(id)).ToList();
        if (removedAddonIds.Count > 0)
        {
            var toRemove = await _context.Addons
                .Where(a => removedAddonIds.Contains(a.Id))
                .ToListAsync();
            _context.Addons.RemoveRange(toRemove);
        }

        var existingMemberIds = await _context.Set<ProjectMember>()
            .Where(m => m.ProjectId == project.Id)
            .Select(m => m.UserId)
            .ToListAsync();

        var currentMemberIds = project.Members.Select(m => m.UserId).ToHashSet();
        var removedMemberIds = existingMemberIds.Where(id => !currentMemberIds.Contains(id)).ToList();
        if (removedMemberIds.Count > 0)
        {
            var membersToRemove = await _context.Set<ProjectMember>()
                .Where(m => m.ProjectId == project.Id && removedMemberIds.Contains(m.UserId))
                .ToListAsync();
            _context.Set<ProjectMember>().RemoveRange(membersToRemove);
        }

        // Attach + точечные скаляры: Update() всего графа метил бы Modified
        // связанный Status и существующие Addon/Member-строки.
        _context.Projects.Attach(project);
        _context.Entry(project).Property(p => p.Name).IsModified = true;
        _context.Entry(project).Property(p => p.Description).IsModified = true;
        _context.Entry(project).Property(p => p.Mode).IsModified = true;
        _context.Entry(project).Property("StatusId").IsModified = true;
        foreach (var addon in project.Addons)
        {
            if (!existingAddonIds.Contains(addon.Id))
                _context.Entry(addon).State = EntityState.Added;
        }
        foreach (var member in project.Members)
        {
            if (!existingMemberIds.Contains(member.UserId))
                _context.Entry(member).State = EntityState.Added;
        }
    }

    public Task UpdateStatusAsync(Project project)
    {
        var tracked = _context.ChangeTracker.Entries<Project>()
            .FirstOrDefault(e => e.Entity.Id == project.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        _context.Attach(project);
        _context.Entry(project).Property("StatusId").IsModified = true;
        return Task.CompletedTask;
    }

    public Task UpdateDetailsAsync(Project project)
    {
        var tracked = _context.ChangeTracker.Entries<Project>()
            .FirstOrDefault(e => e.Entity.Id == project.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        _context.Attach(project);
        _context.Entry(project).Property(p => p.Name).IsModified = true;
        _context.Entry(project).Property(p => p.Description).IsModified = true;
        _context.Entry(project).Property(p => p.Mode).IsModified = true;
        return Task.CompletedTask;
    }

    public async Task UpdateMembersAsync(Project project)
    {
        var tracked = _context.ChangeTracker.Entries<Project>()
            .FirstOrDefault(e => e.Entity.Id == project.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        var existingMemberIds = await _context.Set<ProjectMember>()
            .Where(m => m.ProjectId == project.Id)
            .Select(m => m.UserId)
            .ToListAsync();

        var currentMemberIds = project.Members.Select(m => m.UserId).ToHashSet();
        var removedMemberIds = existingMemberIds.Where(id => !currentMemberIds.Contains(id)).ToList();
        if (removedMemberIds.Count > 0)
        {
            var membersToRemove = await _context.Set<ProjectMember>()
                .Where(m => m.ProjectId == project.Id && removedMemberIds.Contains(m.UserId))
                .ToListAsync();
            _context.Set<ProjectMember>().RemoveRange(membersToRemove);
        }

        _context.Attach(project);
        foreach (var member in project.Members)
        {
            if (!existingMemberIds.Contains(member.UserId))
                _context.Entry(member).State = EntityState.Added;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            _context.Projects.Remove(project);
        }
    }

    public async Task<Guid?> GetProjectIdByAddonAsync(Guid addonId)
    {
        var projectId = await _context.Addons
            .Where(a => a.Id == addonId)
            .Select(a => EF.Property<Guid>(a, "ProjectId"))
            .FirstOrDefaultAsync();
        return projectId == Guid.Empty ? null : projectId;
    }
}
