using System.Net.Http.Headers;
using System.Net.Http.Json;
using FanFundingBot.App.Auth;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components;

public static class WebApi
{
    public static HttpClient Client(IHttpClientFactory factory, NavigationManager nav, WebApiTokenAccessor? accessor = null)
    {
        var client = factory.CreateClient("webapi");
        // Loopback через ngrok теряет Authorization при http→https редиректе — форсим https для ngrok.
        var baseUri = nav.BaseUri;
        if (baseUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && baseUri.Contains("ngrok", StringComparison.OrdinalIgnoreCase))
            baseUri = "https://" + baseUri.Substring("http://".Length);
        client.BaseAddress = new Uri(baseUri);
        if (accessor is not null && !string.IsNullOrEmpty(accessor.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessor.Token);
        return client;
    }

    public static HttpClient Client(IHttpClientFactory factory, NavigationManager nav, string? token)
    {
        var client = factory.CreateClient("webapi");
        var baseUri = nav.BaseUri;
        if (baseUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && baseUri.Contains("ngrok", StringComparison.OrdinalIgnoreCase))
            baseUri = "https://" + baseUri.Substring("http://".Length);
        client.BaseAddress = new Uri(baseUri);
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
