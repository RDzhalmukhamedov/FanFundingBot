using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.Tests.App;

/// <summary>
/// Blazor-страницы резолвят эти сервисы из DI в рантайме (компилятор это не ловит,
/// см. инцидент с BlazorCircuitUserContext). Тест падает до запуска приложения.
/// </summary>
[TestFixture]
public class FluentServicesTests
{
    [Test]
    public void ToastService_Resolves()
    {
        Resolve<IToastService>().Should().NotBeNull();
    }

    [Test]
    public void DialogService_Resolves()
    {
        Resolve<IDialogService>().Should().NotBeNull();
    }

    private static T Resolve<T>() where T : class
    {
        var services = new ServiceCollection();
        services.AddFluentUIComponents();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
