namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Json;
using DocMind.Api.Contracts;
using Microsoft.AspNetCore.Identity.Data;

[Collection(ApiCollectionDefinition.Name)]
public class AuthEndpointTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task QueryWithoutSessionReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/query", new QueryRequest("What is DocMind?", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task UploadWithoutSessionReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/documents/upload", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithCookiesExposesCurrentUser()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync(
            "/auth/login?useCookies=true",
            new LoginRequest { Email = email, Password = DocMindApiFactory.TestPassword });
        var info = await client.GetFromJsonAsync<InfoResponse>("/auth/manage/info");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(email, info?.Email);
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync(
            "/auth/login?useCookies=true",
            new LoginRequest { Email = email, Password = "Wr0ngPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task RegisterDuplicateEmailReturnsBadRequest()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var duplicate = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest { Email = email, Password = DocMindApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task LogoutEndsSession()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var logout = await client.PostAsync("/auth/logout", content: null);
        var info = await client.GetAsync("/auth/manage/info");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, info.StatusCode);
    }

    private static string NewEmail() => $"{Guid.NewGuid()}@test.local";

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest { Email = email, Password = DocMindApiFactory.TestPassword });
        _ = response.EnsureSuccessStatusCode();
    }
}