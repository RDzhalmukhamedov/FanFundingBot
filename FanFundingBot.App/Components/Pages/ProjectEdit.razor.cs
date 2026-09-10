using System.Net.Http.Json;
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

    private ProjectDto? Project { get; set; }
    private string Name { get; set; } = string.Empty;
    private string Description { get; set; } = string.Empty;
    private List<AddonForm> Addons { get; set; } = [];
    private string NewName { get; set; } = string.Empty;
    private string NewDescription { get; set; } = string.Empty;
    private decimal NewPrice { get; set; }
    private string? Error { get; set; }
    private Guid? DeleteAddonTarget { get; set; }
    private string? DeleteError { get; set; }

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
        var client = WebApi.Client(HttpFactory, Nav);
        Project = await client.GetFromJsonAsync<ProjectDto>($"api/projects/{Id}");
        if (Project is null)
            return;

        Name = Project.Name;
        Description = Project.Description ?? string.Empty;
        Addons = Project.Addons
            .Select(a => new AddonForm { Id = a.Id, Name = a.Name, Description = a.Description ?? string.Empty, Price = a.Price })
            .ToList();
    }

    private async Task SaveAsync()
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PutAsJsonAsync($"api/projects/{Id}", new { name = Name, description = Description });
        if (response.IsSuccessStatusCode)
            await ReloadAsync();
        else
            Error = await WebApi.ReadErrorAsync(response);
    }

    private async Task SaveAddonAsync(AddonForm addon)
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PutAsJsonAsync($"api/addons/{addon.Id}", new
        {
            name = addon.Name,
            description = addon.Description,
            price = addon.Price
        });

        if (response.IsSuccessStatusCode)
            await ReloadAsync();
        else
            Error = await WebApi.ReadErrorAsync(response);
    }

    private async Task AddAddonAsync()
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PostAsJsonAsync($"api/projects/{Id}/addons", new
        {
            name = NewName,
            description = NewDescription,
            price = NewPrice
        });

        if (response.IsSuccessStatusCode)
        {
            NewName = string.Empty;
            NewDescription = string.Empty;
            NewPrice = 0;
            await ReloadAsync();
        }
        else
        {
            Error = await WebApi.ReadErrorAsync(response);
        }
    }

    private void AskDeleteAddon(Guid addonId) { DeleteAddonTarget = addonId; DeleteError = null; }
    private void CancelDeleteAddon() { DeleteAddonTarget = null; DeleteError = null; }

    private async Task DeleteAddonAsync()
    {
        if (!DeleteAddonTarget.HasValue)
            return;

        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.DeleteAsync($"api/addons/{DeleteAddonTarget.Value}");
        if (response.IsSuccessStatusCode)
        {
            DeleteAddonTarget = null;
            await ReloadAsync();
        }
        else
        {
            DeleteError = await WebApi.ReadErrorAsync(response);
        }
    }

    private sealed class AddonForm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
