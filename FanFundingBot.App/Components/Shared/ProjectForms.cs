using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.App.Components.Shared;

/// <summary>
/// Общие модели форм проектов/дополнений (были продублированы в ProjectCreate/ProjectEdit).
/// Клиентские правила 1:1 с серверными FluentValidation (см. CreateProjectCommand/AddonCommands).
/// </summary>
public static class ProjectForms
{
    public static string? ValidateAddons(IEnumerable<AddonForm> addons)
    {
        foreach (var addon in addons)
        {
            if (string.IsNullOrWhiteSpace(addon.Name))
                return "Укажите название дополнения.";
            if (addon.Name.Length > 256)
                return "Название дополнения длиннее 256 символов.";
            if ((addon.Description?.Length ?? 0) > 2048)
                return "Описание дополнения длиннее 2048 символов.";
            if (addon.Price < 0)
                return "Цена дополнения не может быть отрицательной.";
        }

        return null;
    }
}

[ExcludeFromCodeCoverage]
public sealed class ProjectForm
{
    [Required(ErrorMessage = "Укажите название.")]
    [MaxLength(256, ErrorMessage = "Название длиннее 256 символов.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите описание.")]
    [MaxLength(2048, ErrorMessage = "Описание длиннее 2048 символов.")]
    public string Description { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public sealed class AddonForm
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Укажите название дополнения.")]
    [MaxLength(256, ErrorMessage = "Название дополнения длиннее 256 символов.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2048, ErrorMessage = "Описание дополнения длиннее 2048 символов.")]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public bool HasOrders { get; set; }
}
