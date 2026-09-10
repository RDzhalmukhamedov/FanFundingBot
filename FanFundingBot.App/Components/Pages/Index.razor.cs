using System.Net.Http.Json;
using FanFundingBot.Application.Projects.DTOs;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class Index
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private string? BotUsername { get; set; }
    private List<ProjectDto>? Catalog { get; set; }

    protected override async Task OnInitializedAsync()
    {
        BotUsername = Configuration["TelegramLogin:BotUsername"];

        try
        {
            var client = WebApi.Client(HttpFactory, Nav);
            Catalog = await client.GetFromJsonAsync<List<ProjectDto>>("projects");
        }
        catch
        {
            Catalog = [];
        }
    }
}
