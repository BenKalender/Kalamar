using System.Net;
using System.Net.Http.Json;
using TaskService.Interfaces;
using TaskService.Models;

namespace TaskService.Services;

public class PersistenceTaskClient(HttpClient httpClient) : IPersistenceTaskClient
{
    public async Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/tasks/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskItem>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tasks = await httpClient.GetFromJsonAsync<List<TaskItem>>("api/tasks", cancellationToken);
        return tasks ?? [];
    }

    public async Task<TaskItem> CreateAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/tasks", taskItem, cancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<TaskItem>(cancellationToken: cancellationToken);
        return created ?? throw new InvalidOperationException("PersistenceService returned an empty response body.");
    }

    public async Task<bool> UpdateAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"api/tasks/{taskItem.Id}", taskItem, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync($"api/tasks/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}