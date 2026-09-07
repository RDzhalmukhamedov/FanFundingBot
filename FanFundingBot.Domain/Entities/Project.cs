using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using System.Collections.Immutable;

namespace FanFundingBot.Domain.Entities;

public class Project : IEditable<Project>
{
    private List<Addon> _addons = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime? CreatedAt { get; private set; }
    public EditMode Mode { get; private set; }
    public ProjectStatus Status { get; private set; } = null!;
    public bool CanEditOrders => Status.CanEditOrders;

    public IReadOnlyList<Addon> Addons => _addons.AsReadOnly();

    public Project()
    {
        Id = Guid.NewGuid();
    }

    public Project(Guid id, string name, string description, DateTime createdAt, EditMode mode, ProjectStatus status, ImmutableArray<Addon> addons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(status);

        Id = id;
        Name = name;
        Description = description;
        CreatedAt = createdAt;
        Mode = mode;
        Status = status;
        _addons = addons.ToList();
    }

    public Project StartEdit()
    {
        Mode = EditMode.Edit;

        return this;
    }

    public Project FinishEdit()
    {
        ThrowIfNotEditMode();
        Mode = EditMode.Formed;
        if (CreatedAt is null) CreatedAt = DateTime.UtcNow;

        return this;
    }

    public void SetName(string name)
    {
        ThrowIfNotEditMode();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void SetDescription(string description)
    {
        ThrowIfNotEditMode();
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }

    public void SetStatus(ProjectStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Status = status;
    }

    public void AddAddon(Addon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        ThrowIfNotEditMode();
        if (_addons.Any(a => a.Id == addon.Id))
        {
            throw new DomainException($"Дополнение «{addon.Name}» уже добавлено в проект.");
        }

        _addons.Add(addon);
    }

    private void ThrowIfNotEditMode()
    {
        if (Mode != EditMode.Edit) throw new ProjectNotEditModeException();
    }
}
