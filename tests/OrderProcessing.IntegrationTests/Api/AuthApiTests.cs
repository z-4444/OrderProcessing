using System.Net;
using System.Net.Http.Json;
using OrderProcessing.Application.Auth;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("SqlServer")]
public sealed class AuthApiTests
{
    private readonly SqlServerFixture _fixture;

    public AuthApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithValidAdmin_ReturnsJwt()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@localhost", "Admin_Pass_123!"));
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiTestHelpers.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body?.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body?.RefreshToken));
        Assert.Contains("Admin", body!.User.Roles);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@localhost", "Wrong_Pass_123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Returns401()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing@localhost", "Admin_Pass_123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();
        var token = await ApiTestHelpers.LoginAsync(client);

        var response = await client.WithBearer(token).GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(ApiTestHelpers.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("admin@localhost", body?.Email);
        Assert.Contains("Admin", body!.Roles);
    }
}
