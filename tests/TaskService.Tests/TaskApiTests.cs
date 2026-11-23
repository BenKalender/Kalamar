using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskService.Interfaces;
using TaskService.Models;

namespace TaskService.Tests;

/// <summary>
/// TaskService'i gerçek HTTP pipeline'ı ile ayağa kaldırır; Persistence ve User servisleri sahte istemcilerle değiştirilir.
/// </summary>
public sealed class TaskApiTests : IDisposable
{
    private const int ExistingUserId = 7;

    private readonly FakePersistenceTaskClient persistence = new();
    private readonly FakeUserServiceClient users = new();
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public TaskApiTests()
    {
        users.ExistingUserIds.Add(ExistingUserId);

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPersistenceTaskClient>(persistence);
                services.AddSingleton<IUserServiceClient>(users);
            }));

        client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_WithExistingUser_Returns201()
    {
        var response = await client.PostAsJsonAsync("api/tasks", NewTask(ExistingUserId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(ExistingUserId, created.UserId);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_WithUnknownUser_Returns400WithUserIdError()
    {
        var response = await client.PostAsJsonAsync("api/tasks", NewTask(userId: 999), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey(nameof(TaskItem.UserId)));
        Assert.Empty(await persistence.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_WhenUserServiceUnavailable_Returns503()
    {
        users.IsUnavailable = true;

        var response = await client.PostAsJsonAsync("api/tasks", NewTask(ExistingUserId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await persistence.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_WithoutChangingUser_DoesNotCallUserService()
    {
        var task = persistence.Seed("existing", userId: 123); // kullanıcısı artık var olmayan eski görev
        users.IsUnavailable = true;
        task.IsCompleted = true;

        var response = await client.PutAsJsonAsync($"api/tasks/{task.Id}", task, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, users.CallCount);
    }

    [Fact]
    public async Task Update_ReassigningToUnknownUser_Returns400()
    {
        var task = persistence.Seed("existing", ExistingUserId);
        task.UserId = 999;

        var response = await client.PutAsJsonAsync($"api/tasks/{task.Id}", task, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await persistence.GetByIdAsync(task.Id, TestContext.Current.CancellationToken);
        Assert.Equal(ExistingUserId, stored!.UserId);
    }

    [Fact]
    public async Task Update_UnknownTask_Returns404()
    {
        var task = NewTask(ExistingUserId);
        task.Id = 4242;

        var response = await client.PutAsJsonAsync("api/tasks/4242", task, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static TaskItem NewTask(int userId) => new() { Title = "Write tests", UserId = userId };

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }
}
