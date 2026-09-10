using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.ValueObjects;

namespace FanFundingBot.Domain.Entities;

public class Addon : IEditable<Addon>
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Price { get; private set; }
    public EditMode Mode { get; private set; }

    public Addon()
    {
        Id = Guid.NewGuid();
        Mode = EditMode.Edit;
    }

    public Addon(Guid id, string name, string description, decimal price, EditMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Id = id;
        Name = name;
        Description = description;
        Price = Money.Create(price);
        Mode = mode;
    }

    public Addon StartEdit()
    {
        Mode = EditMode.Edit;

        return this;
    }

    public Addon FinishEdit()
    {
        ThrowIfNotEditMode();
        Mode = EditMode.Formed;

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
        ArgumentNullException.ThrowIfNull(description);
        Description = description;
    }

    public void SetPrice(decimal price)
    {
        ThrowIfNotEditMode();
        Price = Money.Create(price);
    }

    private void ThrowIfNotEditMode()
    {
        if (Mode != EditMode.Edit) throw new AddonNotEditModeException();
    }
}
