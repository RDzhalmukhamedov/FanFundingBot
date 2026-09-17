using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Domain.Interfaces;

public interface IProjectStatusRepository
{
    Task<List<ProjectStatus>> GetAllAsync();
    Task<int> GetMaxOrderAsync();
    Task<ProjectStatus?> GetByIdAsync(int id);
    Task AddAsync(ProjectStatus entry);

    /// <summary>
    /// Атомарное создание с подбором свободного <c>Order</c> (retry×3 на unique-конфликт).
    /// В отличие от <c>AddAsync</c> (только трекинг, коммит через UoW) — сразу сохраняет,
    /// т.к. retry поверх отложенного UoW-коммита невозможен (контекст отравлен после
    /// упавшего SaveChanges). Исчерпание попыток — портовое исключение конфликта.
    /// </summary>
    Task<ProjectStatus> AddWithNextOrderAsync(
        string displayName, bool canEditOrders, CancellationToken cancellationToken = default);
}
