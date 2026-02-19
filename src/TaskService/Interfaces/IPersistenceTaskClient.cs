using TaskService.Models;

namespace TaskService.Interfaces;

public interface IPersistenceTaskClient
{
    Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken);
    Task<TaskItem> CreateAsync(TaskItem taskItem, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(TaskItem taskItem, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}