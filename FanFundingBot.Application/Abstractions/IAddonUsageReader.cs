namespace FanFundingBot.Application.Abstractions;

public interface IAddonUsageReader
{
    Task<bool> HasOrdersAsync(Guid addonId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, bool>> GetHasOrdersMapAsync(IEnumerable<Guid> addonIds, CancellationToken cancellationToken = default);
}
