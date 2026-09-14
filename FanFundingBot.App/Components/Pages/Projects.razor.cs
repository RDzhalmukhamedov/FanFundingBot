using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class Projects : AuthComponentBase
{
    [Inject] private ILogger<Projects> Logger { get; set; } = null!;

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
        if (!IsAuthenticated(state.User))
        {
            RedirectToLogin();
            return;
        }

        try
        {
            var uid = await UseUserAsync();
            var manageable = await Mediator.Send(new GetManageableProjectsQuery(uid), RequestCt);
            if (manageable.IsSuccess)
            {
                Manageable = manageable.Value;
                IsAdmin = state.User.IsInRole("Admin") || Manageable.Count > 0;
                if (!IsAdmin)
                {
                    var catalog = await Mediator.Send(new GetProjectsQuery(), RequestCt);
                    if (catalog.IsSuccess)
                        Catalog = catalog.Value;
                    else
                        Error = BlazorResult.Message(catalog);
                }
            }
            else
            {
                Error = BlazorResult.Message(manageable);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Projects load failed");
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

        var uid = await UseUserAsync();
        var result = await Mediator.Send(new DeleteProjectCommand(uid, DeleteTarget.Value), RequestCt);
        if (result.IsSuccess)
        {
            DeleteTarget = null;
            DeleteError = null;
            var manageable = await Mediator.Send(new GetManageableProjectsQuery(uid), RequestCt);
            if (manageable.IsSuccess)
                Manageable = manageable.Value;
            else
                DeleteError = BlazorResult.Message(manageable);
        }
        else
        {
            DeleteError = BlazorResult.Message(result);
        }
    }
}
