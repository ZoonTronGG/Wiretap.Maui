using System.Text;
using System.Text.Json;
using Wiretap.Maui.UI;

namespace Wiretap.Maui.Sample;

public partial class MainPage : ContentPage
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Random _random = new();

    public MainPage(IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _httpClientFactory = httpClientFactory;
    }

    private async void OnOpenInspectorClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(WiretapPage));
    }

    private async void OnGetPostsClicked(object? sender, EventArgs e)
    {
        await MakeRequest("GET", "posts?_limit=5");
    }

    private async void OnGetSinglePostClicked(object? sender, EventArgs e)
    {
        await MakeRequest("GET", "posts/1");
    }

    private async void OnCreatePostClicked(object? sender, EventArgs e)
    {
        var payload = new { title = "Test Post", body = "This is a test post from Wiretap.Maui", userId = 1 };
        await MakeRequest("POST", "posts", payload);
    }

    private async void OnUpdatePostClicked(object? sender, EventArgs e)
    {
        var payload = new { id = 1, title = "Updated Post", body = "This post was updated", userId = 1 };
        await MakeRequest("PUT", "posts/1", payload);
    }

    private async void OnDeletePostClicked(object? sender, EventArgs e)
    {
        await MakeRequest("DELETE", "posts/1");
    }

    private async void OnGet404Clicked(object? sender, EventArgs e)
    {
        await MakeRequest("GET", "invalid-endpoint-404");
    }

    private async void OnMakeBatchRequestsClicked(object? sender, EventArgs e)
    {
        ResultLabel.Text = "Making 5 random requests...";

        var endpoints = new[]
        {
            ("GET", "posts/1"),
            ("GET", "posts/2"),
            ("GET", "users/1"),
            ("GET", "comments?postId=1"),
            ("POST", "posts"),
            ("GET", "todos/1"),
            ("GET", "albums/1"),
        };

        for (int i = 0; i < 5; i++)
        {
            var (method, endpoint) = endpoints[_random.Next(endpoints.Length)];
            object? body = method == "POST"
                ? new { title = $"Batch Post {i + 1}", body = "Created in batch", userId = 1 }
                : null;

            try
            {
                var client = _httpClientFactory.CreateClient("DemoApi");

                if (method == "POST")
                    await client.PostAsync(endpoint, CreateJsonContent(body));
                else
                    await client.GetAsync(endpoint);
            }
            catch
            {
                // Ignore errors in batch
            }

            // Small delay between requests
            await Task.Delay(100);
        }

        ResultLabel.Text = "✅ Made 5 requests!\n\nTap the 📡 button or 'Open HTTP Inspector' to view them.";
    }

    private async Task MakeRequest(string method, string endpoint, object? body = null)
    {
        try
        {
            ResultLabel.Text = $"Making {method} request to /{endpoint}...";

            var client = _httpClientFactory.CreateClient("DemoApi");
            HttpResponseMessage response;

            switch (method.ToUpperInvariant())
            {
                case "GET":
                    response = await client.GetAsync(endpoint);
                    break;
                case "POST":
                    response = await client.PostAsync(endpoint, CreateJsonContent(body));
                    break;
                case "PUT":
                    response = await client.PutAsync(endpoint, CreateJsonContent(body));
                    break;
                case "DELETE":
                    response = await client.DeleteAsync(endpoint);
                    break;
                default:
                    ResultLabel.Text = $"Unknown method: {method}";
                    return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var statusEmoji = response.IsSuccessStatusCode ? "✅" : "❌";

            // Pretty print JSON if possible
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(content);
                content = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // Not JSON, use raw content
            }

            // Truncate long responses for display
            if (content.Length > 500)
            {
                content = content[..500] + "\n... (truncated)";
            }

            ResultLabel.Text = $"{statusEmoji} {method} /{endpoint}\n" +
                               $"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                               $"{content}";
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"❌ Error: {ex.Message}";
        }
    }

    private static StringContent CreateJsonContent(object? body)
    {
        if (body == null) return new StringContent("{}", Encoding.UTF8, "application/json");

        var json = JsonSerializer.Serialize(body);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
