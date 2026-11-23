using System.Net;
using Polly;
using TaskService.Interfaces;

namespace TaskService.Services;

public class UserServiceClient(HttpClient httpClient) : IUserServiceClient
{
    public async Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync($"api/users/{userId}", cancellationToken);
        }
        catch (Exception ex) when (IsUnavailable(ex, cancellationToken))
        {
            throw new UserServiceUnavailableException("UserService is unavailable.", ex);
        }

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => true,
                HttpStatusCode.NotFound => false,
                _ => throw new UserServiceUnavailableException(
                    $"UserService returned an unexpected status code: {(int)response.StatusCode}."),
            };
        }
    }

    // HttpRequestException: bağlantı hatası; ExecutionRejectedException: Polly timeout / açık circuit breaker;
    // TaskCanceledException: çağıranın iptali değilse HttpClient timeout'u.
    private static bool IsUnavailable(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        HttpRequestException or ExecutionRejectedException => true,
        TaskCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };
}
