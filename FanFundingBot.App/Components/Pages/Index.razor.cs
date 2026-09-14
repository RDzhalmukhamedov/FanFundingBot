using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class Index : AuthComponentBase
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;

    private string? BotUsername { get; set; }
    private List<ProjectDto>? Catalog { get; set; }

    protected override async Task OnInitializedAsync()
    {
        BotUsername = Configuration["TelegramLogin:BotUsername"];

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
