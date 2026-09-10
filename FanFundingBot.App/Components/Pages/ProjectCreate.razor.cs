using System.Net.Http.Json;
using FanFundingBot.Application.Projects.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components.Pages;

public partial class ProjectCreate
{
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private string Name { get; set; } = string.Empty;
    private string Description { get; set; } = string.Empty;
    private List<AddonForm> Addons { get; set; } = [];
    private string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
            Nav.NavigateTo("/", forceLoad: true);
    }

    private void AddAddon() => Addons.Add(new AddonForm());
    private void RemoveAddon(int index) => Addons.RemoveAt(index);

    private async Task SaveAsync()
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PostAsJsonAsync("api/projects", new
        {
            name = Name,
            description = Description,
            addons = Addons.Select(a => new NewAddonItem(a.Name, a.Description, a.Price)).ToList()
        });

        if (response.IsSuccessStatusCode)
            Nav.NavigateTo("/projects");
        else
            Error = await WebApi.ReadErrorAsync(response);
    }

    private sealed class AddonForm
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
