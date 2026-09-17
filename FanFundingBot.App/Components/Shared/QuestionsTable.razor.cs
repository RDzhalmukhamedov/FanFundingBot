using FanFundingBot.Application.Questions.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class QuestionsTable
{
    [Parameter, EditorRequired] public GridItemsProvider<QuestionDto>? ItemsProvider { get; set; }
    [Parameter, EditorRequired] public PaginationState Pagination { get; set; } = new() { ItemsPerPage = 20 };
    [Parameter] public EventCallback<QuestionDto> OnAnswer { get; set; }

    private FluentDataGrid<QuestionDto>? _grid;

    public Task RefreshAsync() => _grid?.RefreshDataAsync() ?? Task.CompletedTask;

    private Task Answer(QuestionDto q) => OnAnswer.InvokeAsync(q);
}
