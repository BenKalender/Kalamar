using System.Net;
using TaskService.Interfaces;
using TaskService.Services;

namespace TaskService.Tests;

public sealed class UserServiceClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task UserExists_MapsStatusCode(HttpStatusCode statusCode, bool expected)
    {
        var client = CreateClient(_ => new HttpResponseMessage(statusCode));

        var exists = await client.UserExistsAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(expected, exists);
    }

    [Fact]
    public async Task UserExists_CallsUsersEndpoint()
    {
        Uri? requested = null;
        var client = CreateClient(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await client.UserExistsAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal("http://user-service/api/users/42", requested?.ToString());
    }

    [Fact]
    public async Task UserExists_OnServerError_ThrowsUnavailable()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => client.UserExistsAsync(1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UserExists_OnConnectionFailure_ThrowsUnavailable()
    {
        var client = CreateClient(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => client.UserExistsAsync(1, TestContext.Current.CancellationToken));
    }

    private static UserServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("http://user-service/") });

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
