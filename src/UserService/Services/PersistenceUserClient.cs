using System.Net;
using System.Net.Http.Json;
using UserService.Interfaces;
using UserService.Models;

namespace UserService.Services;

public class PersistenceUserClient(HttpClient httpClient) : IPersistenceUserClient
{
    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/users/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<User>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await httpClient.GetFromJsonAsync<List<User>>("api/users", cancellationToken);
        return users ?? [];
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/users", user, cancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<User>(cancellationToken: cancellationToken);
        return created ?? throw new InvalidOperationException("PersistenceService returned an empty response body.");
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"api/users/{user.Id}", user, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync($"api/users/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
