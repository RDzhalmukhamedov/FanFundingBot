using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Application.Orders;

/// <summary>
/// Подсчет превью заказа из эфемерного состояния Bot-сессии.
/// Источник формулы — <see cref="Order.CalculateTotal"/>.
/// </summary>
public static class OrderPreview
{
    public sealed record Line(Guid AddonId, string Name, decimal Price, int Quantity)
    {
        public decimal Total => Price * Quantity;
    }

    public static (IReadOnlyList<Line> Lines, decimal Total) Build(
        ProjectDto project,
        IEnumerable<Guid> selectedAddonIds,
        IReadOnlyDictionary<Guid, int> quantities)
    {
        var lines = new List<Line>();
        foreach (var addonId in selectedAddonIds)
        {
            var addon = project.Addons.FirstOrDefault(a => a.Id == addonId);
            if (addon is null)
                continue;

            var qty = quantities.TryGetValue(addonId, out var q) ? q : 1;
            lines.Add(new Line(addon.Id, addon.Name, addon.Price, qty));
        }

        return (lines, (decimal)Order.CalculateTotal(lines.Select(l => (l.Price, l.Quantity))));
    }
}
