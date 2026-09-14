using FanFundingBot.Application.Questions.DTOs;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class QuestionsTable
{
    [Parameter, EditorRequired] public List<QuestionDto> Questions { get; set; } = [];
    [Parameter] public EventCallback<QuestionDto> OnAnswer { get; set; }

    private Task Answer(QuestionDto q) => OnAnswer.InvokeAsync(q);
}
