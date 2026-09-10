using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components;

public static class WebApi
{
    public static HttpClient Client(IHttpClientFactory factory, NavigationManager nav)
    {
        var client = factory.CreateClient("webapi");
        client.BaseAddress = new Uri(nav.BaseUri);
        return client;
    }

    public static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var messages = await response.Content.ReadFromJsonAsync<List<string>>();
            if (messages is { Count: > 0 })
                return string.Join("; ", messages);
        }
        catch
        {
            // Ниже — fallback на статус-код.
        }

        return $"Ошибка {(int)response.StatusCode}.";
    }
}
