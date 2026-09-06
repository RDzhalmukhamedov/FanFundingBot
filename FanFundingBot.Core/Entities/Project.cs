using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Exceptions;
using System.Collections.Immutable;

namespace FanFundingBot.Core.Entities;

public abstract class Project : IEditable<Project>
{
    private Dictionary<Guid, Addon> _addons = [];

    public Guid Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime? CreatedAt { get; private set; }
    public EditMode Mode { get; private set; }
    public ProjectStatus Status { get; private set; }
    public bool CanEditOrders => Status.CanEditOrders;

    public IReadOnlyDictionary<Guid, Addon> Addons => _addons.AsReadOnly();

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
        _addons = addons.ToDictionary(a => a.Id);
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

    private void ThrowIfNotEditMode()
    {
        if (Mode != EditMode.Edit) throw new ProjectNotEditModeException();
    }
}
