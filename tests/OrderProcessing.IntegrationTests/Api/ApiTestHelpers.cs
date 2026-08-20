using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrderProcessing.Application.Auth;

namespace OrderProcessing.IntegrationTests.Api;

internal static class ApiTestHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<string> LoginAsync(
        HttpClient client,
        string email = "admin@localhost",
        string password = "Admin_Pass_123!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return body?.AccessToken ?? throw new InvalidOperationException("Login did not return a token.");
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
