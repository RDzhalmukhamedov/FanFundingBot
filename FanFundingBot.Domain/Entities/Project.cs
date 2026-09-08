using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using System.Collections.Immutable;

namespace FanFundingBot.Domain.Entities;

public class Project : AggregateRoot, IEditable<Project>
{
    private List<Addon> _addons = [];
    private List<ProjectMember> _members = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime? CreatedAt { get; private set; }
    public EditMode Mode { get; private set; }
    public ProjectStatus Status { get; private set; } = null!;
    public bool CanEditOrders => Status.CanEditOrders;

    public IReadOnlyList<Addon> Addons => _addons.AsReadOnly();
    public IReadOnlyList<ProjectMember> Members => _members.AsReadOnly();

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
        if (ReferenceEquals(Status, status))
            return;

        if (Status is not null && Status.Id != 0 && Status.Id == status.Id)
            return;

        Status = status;

        AddDomainEvent(new ProjectStatusChangedEvent(Id, status.Id, status.DisplayName));
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

    public void RemoveAddon(Guid addonId, bool hasOrders)
    {
        ThrowIfNotEditMode();
        var addon = _addons.FirstOrDefault(a => a.Id == addonId)
            ?? throw new DomainException("Дополнение не найдено.");
        if (hasOrders)
            throw new DomainException("Нельзя удалить дополнение, так как оно есть в заказах.");

        _addons.Remove(addon);
    }

    public void UpdateAddonName(Guid addonId, string name)
    {
        ThrowIfNotEditMode();
        var addon = _addons.FirstOrDefault(a => a.Id == addonId)
            ?? throw new DomainException("Дополнение не найдено.");

        addon.StartEdit();
        addon.SetName(name);
        addon.FinishEdit();
    }

    public void UpdateAddonDescription(Guid addonId, string description)
    {
        ThrowIfNotEditMode();
        var addon = _addons.FirstOrDefault(a => a.Id == addonId)
            ?? throw new DomainException("Дополнение не найдено.");

        addon.StartEdit();
        addon.SetDescription(description);
        addon.FinishEdit();
    }

    public void UpdateAddonPrice(Guid addonId, decimal price)
    {
        ThrowIfNotEditMode();
        var addon = _addons.FirstOrDefault(a => a.Id == addonId)
            ?? throw new DomainException("Дополнение не найдено.");

        addon.StartEdit();
        addon.SetPrice(price);
        addon.FinishEdit();
    }

    public void AddMember(long userId, ProjectRole role)
    {
        if (_members.Any(m => m.UserId == userId))
        {
            throw new DomainException($"Пользователь {userId} уже добавлен в проект.");
        }

        var member = new ProjectMember(userId, role);
        _members.Add(member);

        AddDomainEvent(new ProjectMemberAddedEvent(Id, userId, role));
    }

    public void RemoveMember(long userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            throw new DomainException($"Пользователь {userId} не найден в проекте.");
        }

        _members.Remove(member);
    }

    public bool IsMember(long userId) => _members.Any(m => m.UserId == userId);

    public ProjectRole? GetMemberRole(long userId)
        => _members.FirstOrDefault(m => m.UserId == userId)?.Role;

    private void ThrowIfNotEditMode()
    {
        if (Mode != EditMode.Edit) throw new ProjectNotEditModeException();
    }
}
