using System.Collections.Concurrent;
using TaskService.Interfaces;
using TaskService.Models;

namespace TaskService.Tests;

internal sealed class FakePersistenceTaskClient : IPersistenceTaskClient
{
    private readonly ConcurrentDictionary<int, TaskItem> tasks = new();
    private int nextId;

    public TaskItem Seed(string title, int userId)
    {
        var task = new TaskItem { Id = Interlocked.Increment(ref nextId), Title = title, UserId = userId };
        tasks[task.Id] = task;
        return Clone(task);
    }

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(tasks.TryGetValue(id, out var task) ? Clone(task) : null);

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TaskItem>>(tasks.Values.Select(Clone).ToList());

    public Task<TaskItem> CreateAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        taskItem.Id = Interlocked.Increment(ref nextId);
        tasks[taskItem.Id] = Clone(taskItem);
        return Task.FromResult(taskItem);
    }

    public Task<bool> UpdateAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        if (!tasks.ContainsKey(taskItem.Id))
        {
            return Task.FromResult(false);
        }

        tasks[taskItem.Id] = Clone(taskItem);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(tasks.TryRemove(id, out _));

    private static TaskItem Clone(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        IsCompleted = task.IsCompleted,
        UserId = task.UserId,
    };
}

internal sealed class FakeUserServiceClient : IUserServiceClient
{
    public HashSet<int> ExistingUserIds { get; } = [];

    public bool IsUnavailable { get; set; }

    public int CallCount { get; private set; }

    public Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        CallCount++;
        return IsUnavailable
            ? throw new UserServiceUnavailableException("UserService is unavailable (fake).")
            : Task.FromResult(ExistingUserIds.Contains(userId));
    }
}
