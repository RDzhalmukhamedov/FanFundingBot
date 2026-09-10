using System.Net.Http.Json;
using FanFundingBot.Application.Projects.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components.Pages;

public partial class Projects
{
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool Loaded { get; set; }
    private bool IsAdmin { get; set; }
    private string? Error { get; set; }
    private List<ProjectDto> Manageable { get; set; } = [];
    private List<ProjectDto> Catalog { get; set; } = [];
    private bool CanCreate => Manageable.Count == 0;
    private Guid? DeleteTarget { get; set; }
    private string? DeleteError { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            Nav.NavigateTo("/", forceLoad: true);
            return;
        }

        try
        {
            var client = WebApi.Client(HttpFactory, Nav);
            Manageable = await client.GetFromJsonAsync<List<ProjectDto>>("api/projects/manageable") ?? [];
            IsAdmin = state.User.IsInRole("Admin") || Manageable.Count > 0;
            if (!IsAdmin)
                Catalog = await client.GetFromJsonAsync<List<ProjectDto>>("projects") ?? [];
        }
        catch
        {
            Error = "Не удалось загрузить проекты. Попробуйте позже.";
        }

        Loaded = true;
    }

    private void Create() => Nav.NavigateTo("/projects/new");
    private void Open(Guid id) => Nav.NavigateTo($"/projects/{id}");
    private void Edit(Guid id) => Nav.NavigateTo($"/projects/{id}/edit");
    private void AskDelete(Guid id) { DeleteTarget = id; DeleteError = null; }
    private void CancelDelete() { DeleteTarget = null; DeleteError = null; }

    private async Task DeleteAsync()
    {
        if (!DeleteTarget.HasValue)
            return;

        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.DeleteAsync($"api/projects/{DeleteTarget.Value}");
        if (response.IsSuccessStatusCode)
        {
            DeleteTarget = null;
            DeleteError = null;
            Manageable = await client.GetFromJsonAsync<List<ProjectDto>>("api/projects/manageable") ?? [];
        }
        else
        {
            DeleteError = await WebApi.ReadErrorAsync(response);
        }
    }
}
