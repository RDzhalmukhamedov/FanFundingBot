using FanFundingBot.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence.Readers;

public class AddonUsageReader : IAddonUsageReader
{
    private readonly AppDbContext _context;

    public AddonUsageReader(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasOrdersAsync(Guid addonId, CancellationToken cancellationToken = default)
    {
        return await _context.OrderPositions.AnyAsync(p => p.AddonId == addonId, cancellationToken);
    }

    public async Task<Dictionary<Guid, bool>> GetHasOrdersMapAsync(IEnumerable<Guid> addonIds, CancellationToken cancellationToken = default)
    {
        var list = addonIds.Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<Guid, bool>();

        var used = await _context.OrderPositions
            .Where(p => list.Contains(p.AddonId))
            .Select(p => p.AddonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usedSet = new HashSet<Guid>(used);
        return list.ToDictionary(id => id, id => usedSet.Contains(id));
    }
}
