using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class ProjectCreate : AuthComponentBase
{
    private string Name { get; set; } = string.Empty;
    private string Description { get; set; } = string.Empty;
    private List<AddonForm> Addons { get; set; } = [];
    private string? Error { get; set; }
    private bool IsSaving { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (!IsAuthenticated(state.User))
            RedirectToLogin();
    }

    private void AddAddon() => Addons.Add(new AddonForm());
    private void RemoveAddon(int index) => Addons.RemoveAt(index);

    private async Task SaveAsync()
    {
        if (IsSaving)
            return;

        IsSaving = true;
        try
        {
            var uid = await UseUserAsync();
            var result = await Mediator.Send(new CreateProjectCommand(
                uid,
                Name,
                Description,
                Addons.Select(a => new NewAddonItem(a.Name, a.Description, a.Price)).ToList()), RequestCt);

            if (result.IsSuccess)
                Nav.NavigateTo("/projects");
            else
                Error = BlazorResult.Message(result);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private sealed class AddonForm
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
