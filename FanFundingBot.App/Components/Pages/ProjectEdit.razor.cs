using FanFundingBot.App.Components.Shared;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class ProjectEdit : AuthComponentBase
{
    [Parameter] public Guid Id { get; set; }

    private ProjectDto? Project { get; set; }
    private bool Loaded { get; set; }
    private ProjectForm Form { get; set; } = new();
    private List<AddonForm> Addons { get; set; } = [];
    private List<AddonForm> NewAddons { get; set; } = [];
    private HashSet<Guid> DeletedAddonIds { get; set; } = [];
    private string? Error { get; set; }
    private bool IsSaving { get; set; }
    [Inject] private IDialogService Dialogs { get; set; } = null!;
    [Inject] private IToastService Toasts { get; set; } = null!;
    private long _userId;

    private string _originalName = string.Empty;
    private string _originalDescription = string.Empty;
    private List<AddonForm> _originalAddons = [];

    private bool IsDirty =>
        Form.Name != _originalName ||
        Form.Description != _originalDescription ||
        DeletedAddonIds.Count > 0 ||
        NewAddons.Count > 0 ||
        Addons.Any(AddonChanged) ||
        NewAddons.Any(n => !string.IsNullOrWhiteSpace(n.Name) || !string.IsNullOrWhiteSpace(n.Description) || n.Price != 0);

    private bool AddonChanged(AddonForm addon)
    {
        var orig = _originalAddons.FirstOrDefault(o => o.Id == addon.Id);
        if (orig is null) return true;
        return addon.Name != orig.Name || addon.Description != orig.Description || addon.Price != orig.Price;
    }

    private void OnFieldChanged() => StateHasChanged();

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (!IsAuthenticated(state.User))
        {
            RedirectToLogin();
            return;
        }

        _userId = await UseUserAsync();
        try
        {
            await ReloadAsync();
        }
        catch
        {
            Error = "Не удалось загрузить проект. Попробуйте позже.";
        }
        finally
        {
            Loaded = true;
        }
    }

    private async Task ReloadAsync()
    {
        var result = await Mediator.Send(new GetProjectByIdQuery(Id), RequestCt);
        if (result.IsFailed)
        {
            Error = BlazorResult.Message(result);
            return;
        }

        Project = result.Value;

        Form.Name = Project.Name;
        Form.Description = Project.Description ?? string.Empty;
        Addons = Project.Addons
            .Select(a => new AddonForm { Id = a.Id, Name = a.Name, Description = a.Description ?? string.Empty, Price = a.Price, HasOrders = a.HasOrders })
            .ToList();
        NewAddons = [];
        DeletedAddonIds = [];
        _originalName = Form.Name;
        _originalDescription = Form.Description;
        _originalAddons = Addons.Select(a => new AddonForm { Id = a.Id, Name = a.Name, Description = a.Description, Price = a.Price, HasOrders = a.HasOrders }).ToList();
        Error = null;
    }

    private void AddNewAddon()
    {
        NewAddons.Add(new AddonForm { Id = Guid.Empty, Name = string.Empty, Description = string.Empty, Price = 0, HasOrders = false });
    }

    private void RemoveNewAddon(int index)
    {
        if (index >= 0 && index < NewAddons.Count)
            NewAddons.RemoveAt(index);
    }

    private async Task AskDeleteAddonAsync(Guid addonId)
    {
        var addon = Addons.FirstOrDefault(a => a.Id == addonId);
        if (addon is null || addon.HasOrders)
        {
            return;
        }

        var dialog = await Dialogs.ShowDialogAsync<ConfirmDialog, ConfirmDialogData>(
            new ConfirmDialogData(
                "Удаление дополнения",
                "Удалить дополнение? Действие будет применено при сохранении."),
            new DialogParameters { Title = "Удаление дополнения" });
        if ((await dialog.Result).Cancelled)
            return;

        Addons.Remove(addon);
        DeletedAddonIds.Add(addonId);
        Toasts.ShowSuccess("Дополнение будет удалено при сохранении.");
    }

    private async Task BackToProjectAsync()
    {
        if (!IsDirty)
        {
            Nav.NavigateTo($"/projects/{Id}");
            return;
        }

        var dialog = await Dialogs.ShowDialogAsync<ConfirmDialog, ConfirmDialogData>(
            new ConfirmDialogData(
                "Несохранённые изменения",
                "Вернуться без сохранения? Несохранённые изменения будут потеряны."),
            new DialogParameters { Title = "Несохранённые изменения" });
        if ((await dialog.Result).Cancelled)
            return;

        Nav.NavigateTo($"/projects/{Id}");
    }

    private async Task SaveAllAsync()
    {
        if (!IsDirty || IsSaving)
            return;

        Error = null;
        IsSaving = true;
        try
        {
            await SaveAllCoreAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveAllCoreAsync()
    {
        // Validate new addons: if any new addon has empty name but other fields filled, show error
        foreach (var ne in NewAddons)
        {
            if (string.IsNullOrWhiteSpace(ne.Name) && (!string.IsNullOrWhiteSpace(ne.Description) || ne.Price != 0))
            {
                Error = "У нового дополнения не заполнено название.";
                return;
            }
        }
        // Filter out completely empty new addons
        var toCreate = NewAddons.Where(n => !string.IsNullOrWhiteSpace(n.Name)).ToList();
        if (toCreate.Count == 0 && NewAddons.Count > 0 && (Form.Name == _originalName && Form.Description == _originalDescription && !Addons.Any(AddonChanged) && DeletedAddonIds.Count == 0))
        {
            // All new addons empty, nothing to create but UI has empty rows - just clear them
            NewAddons.Clear();
            return;
        }

        Error = ProjectForms.ValidateAddons(Addons.Concat(toCreate));
        if (Error is not null)
            return;

        // Save project details if changed (два вызова: Rename + UpdateDescription).
        if (Form.Name != _originalName || Form.Description != _originalDescription)
        {
            var renamed = await Mediator.Send(new RenameProjectCommand(_userId, Id, Form.Name), RequestCt);
            if (renamed.IsFailed)
            {
                Error = BlazorResult.Message(renamed);
                return;
            }

            var described = await Mediator.Send(new UpdateProjectDescriptionCommand(_userId, Id, Form.Description), RequestCt);
            if (described.IsFailed)
            {
                Error = BlazorResult.Message(described);
                return;
            }
        }

        // Save existing addons that changed
        foreach (var addon in Addons)
        {
            var orig = _originalAddons.FirstOrDefault(o => o.Id == addon.Id);
            if (orig is null) continue;
            bool nameChanged = addon.Name != orig.Name;
            bool descChanged = addon.Description != orig.Description;
            bool priceChanged = addon.Price != orig.Price;
            if (!nameChanged && !descChanged && !priceChanged) continue;

            if (nameChanged)
            {
                var renamed = await Mediator.Send(new UpdateAddonNameCommand(_userId, addon.Id, addon.Name), RequestCt);
                if (renamed.IsFailed)
                {
                    Error = BlazorResult.Message(renamed);
                    return;
                }
            }

            if (descChanged)
            {
                var described = await Mediator.Send(new UpdateAddonDescriptionCommand(_userId, addon.Id, addon.Description), RequestCt);
                if (described.IsFailed)
                {
                    Error = BlazorResult.Message(described);
                    return;
                }
            }

            if (priceChanged)
            {
                var priced = await Mediator.Send(new UpdateAddonPriceCommand(_userId, addon.Id, addon.Price), RequestCt);
                if (priced.IsFailed)
                {
                    Error = BlazorResult.Message(priced);
                    return;
                }
            }
        }

        // Create new addons
        foreach (var ne in toCreate)
        {
            var created = await Mediator.Send(new CreateAddonCommand(_userId, Id, ne.Name, ne.Description, ne.Price), RequestCt);
            if (created.IsFailed)
            {
                Error = BlazorResult.Message(created);
                return;
            }
        }

        // Delete addons
        foreach (var delId in DeletedAddonIds.ToList())
        {
            var deleted = await Mediator.Send(new DeleteAddonCommand(_userId, delId), RequestCt);
            if (deleted.IsFailed)
            {
                Error = BlazorResult.Message(deleted);
                return;
            }
        }

        await ReloadAsync();
    }
}
