using System.Net.Http.Json;
using FanFundingBot.App.Auth;
using FanFundingBot.Application.Projects.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components.Pages;

public partial class ProjectEdit
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private WebApiTokenAccessor TokenAccessor { get; set; } = null!;

    private ProjectDto? Project { get; set; }
    private string Name { get; set; } = string.Empty;
    private string Description { get; set; } = string.Empty;
    private List<AddonForm> Addons { get; set; } = [];
    private List<AddonForm> NewAddons { get; set; } = [];
    private HashSet<Guid> DeletedAddonIds { get; set; } = [];
    private string? Error { get; set; }
    private Guid? DeleteAddonTarget { get; set; }
    private string? DeleteError { get; set; }

    private string _originalName = string.Empty;
    private string _originalDescription = string.Empty;
    private List<AddonForm> _originalAddons = [];

    private bool IsDirty =>
        Name != _originalName ||
        Description != _originalDescription ||
        DeletedAddonIds.Count > 0 ||
        NewAddons.Count > 0 ||
        Addons.Any(a =>
        {
            var orig = _originalAddons.FirstOrDefault(o => o.Id == a.Id);
            if (orig is null) return true;
            return a.Name != orig.Name || a.Description != orig.Description || a.Price != orig.Price;
        }) ||
        NewAddons.Any(n => !string.IsNullOrWhiteSpace(n.Name) || !string.IsNullOrWhiteSpace(n.Description) || n.Price != 0);

    private void OnFieldChanged() => StateHasChanged();

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            Nav.NavigateTo("/", forceLoad: true);
            return;
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var client = WebApi.Client(HttpFactory, Nav, TokenAccessor);
        Project = await client.GetFromJsonAsync<ProjectDto>($"api/projects/{Id}");
        if (Project is null)
            return;

        Name = Project.Name;
        Description = Project.Description ?? string.Empty;
        Addons = Project.Addons
            .Select(a => new AddonForm { Id = a.Id, Name = a.Name, Description = a.Description ?? string.Empty, Price = a.Price, HasOrders = a.HasOrders })
            .ToList();
        NewAddons = [];
        DeletedAddonIds = [];
        _originalName = Name;
        _originalDescription = Description;
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

    private void AskDeleteAddon(Guid addonId)
    {
        var addon = Addons.FirstOrDefault(a => a.Id == addonId);
        if (addon is not null && addon.HasOrders)
        {
            return;
        }
        DeleteAddonTarget = addonId;
        DeleteError = null;
    }
    private void CancelDeleteAddon() { DeleteAddonTarget = null; DeleteError = null; }

    private void ConfirmDeleteAddon()
    {
        if (!DeleteAddonTarget.HasValue)
            return;

        var id = DeleteAddonTarget.Value;
        var addon = Addons.FirstOrDefault(a => a.Id == id);
        if (addon is not null)
        {
            if (addon.HasOrders)
            {
                DeleteError = "Это дополнение есть в одном или нескольких заказах.";
                return;
            }
            Addons.Remove(addon);
            DeletedAddonIds.Add(id);
        }
        DeleteAddonTarget = null;
        DeleteError = null;
    }

    private async Task SaveAllAsync()
    {
        if (!IsDirty)
            return;

        Error = null;
        var client = WebApi.Client(HttpFactory, Nav, TokenAccessor);

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
        if (toCreate.Count == 0 && NewAddons.Count > 0 && (Name == _originalName && Description == _originalDescription && !Addons.Any(a => { var o=_originalAddons.FirstOrDefault(x=>x.Id==a.Id); return o is null || o.Name!=a.Name || o.Description!=a.Description || o.Price!=a.Price; }) && DeletedAddonIds.Count==0))
        {
            // All new addons empty, nothing to create but UI has empty rows - just clear them
            NewAddons.Clear();
            return;
        }

        // Save project details if changed
        if (Name != _originalName || Description != _originalDescription)
        {
            var response = await client.PutAsJsonAsync($"api/projects/{Id}", new { name = Name, description = Description });
            if (!response.IsSuccessStatusCode)
            {
                Error = await WebApi.ReadErrorAsync(response);
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

            var payload = new
            {
                name = nameChanged ? addon.Name : null,
                description = descChanged ? addon.Description : null,
                price = priceChanged ? (decimal?)addon.Price : null
            };
            var response = await client.PutAsJsonAsync($"api/addons/{addon.Id}", payload);
            if (!response.IsSuccessStatusCode)
            {
                Error = await WebApi.ReadErrorAsync(response);
                return;
            }
        }

        // Create new addons
        foreach (var ne in toCreate)
        {
            var response = await client.PostAsJsonAsync($"api/projects/{Id}/addons", new
            {
                name = ne.Name,
                description = ne.Description,
                price = ne.Price
            });
            if (!response.IsSuccessStatusCode)
            {
                Error = await WebApi.ReadErrorAsync(response);
                return;
            }
        }

        // Delete addons
        foreach (var delId in DeletedAddonIds.ToList())
        {
            var response = await client.DeleteAsync($"api/addons/{delId}");
            if (!response.IsSuccessStatusCode)
            {
                Error = await WebApi.ReadErrorAsync(response);
                return;
            }
        }

        await ReloadAsync();
    }

    private sealed class AddonForm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool HasOrders { get; set; }
    }
}
