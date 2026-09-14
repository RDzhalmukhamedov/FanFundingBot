using FanFundingBot.Application.Questions.DTOs;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class QuestionsTable
{
    [Parameter, EditorRequired] public List<QuestionDto> Questions { get; set; } = [];
    [Parameter, EditorRequired] public Dictionary<Guid, string> Answers { get; set; } = new();
    [Parameter] public EventCallback<Guid> OnAnswer { get; set; }

    private Task Answer(QuestionDto q) => OnAnswer.InvokeAsync(q.Id);

    private void OnInput(Guid id, string value)
    {
        Answers[id] = value;
    }
}
