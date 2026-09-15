using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class Index : AuthComponentBase
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;

    private string? ClientId { get; set; }
    private List<ProjectDto>? Catalog { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ClientId = Configuration["BotConfiguration:ClientId"]
            ?? Configuration["TelegramLogin:ClientId"];

        var state = await AuthState.GetAuthenticationStateAsync();
        if (!IsAuthenticated(state.User))
        {
            Catalog = [];
            return;
        }

        // Граница: неожиданный сбой не должен ронять лендинг.
        try
        {
            var result = await Mediator.Send(new GetProjectsQuery(), RequestCt);
            Catalog = result.IsSuccess ? result.Value : [];
        }
        catch
        {
            Catalog = [];
        }
    }
}
