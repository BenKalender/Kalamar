using Microsoft.AspNetCore.Mvc;
using TaskService.Interfaces;
using TaskService.Models;

namespace TaskService.Controllers;

[Route("api/tasks")]
[ApiController]
public class TaskController(
    IPersistenceTaskClient persistenceTaskClient,
    IUserServiceClient userServiceClient) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetTask(int id, CancellationToken cancellationToken)
    {
        var taskItem = await persistenceTaskClient.GetByIdAsync(id, cancellationToken);
        return taskItem is null ? NotFound() : Ok(taskItem);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItem>>> GetAllTasks(CancellationToken cancellationToken)
    {
        var tasks = await persistenceTaskClient.GetAllAsync(cancellationToken);
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> AddTask(TaskItem taskItem, CancellationToken cancellationToken)
    {
        var userCheck = await ValidateUserAsync(taskItem.UserId, cancellationToken);
        if (userCheck is not null)
        {
            return userCheck;
        }

        var createdTask = await persistenceTaskClient.CreateAsync(taskItem, cancellationToken);
        return CreatedAtAction(nameof(GetTask), new { id = createdTask.Id }, createdTask);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, TaskItem taskItem, CancellationToken cancellationToken)
    {
        if (id != taskItem.Id)
        {
            return BadRequest();
        }

        var existing = await persistenceTaskClient.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        // Yalnızca görev başka bir kullanıcıya atanıyorsa doğrula; böylece kullanıcısı silinmiş
        // eski bir görev hâlâ "tamamlandı" olarak işaretlenebilir.
        if (existing.UserId != taskItem.UserId)
        {
            var userCheck = await ValidateUserAsync(taskItem.UserId, cancellationToken);
            if (userCheck is not null)
            {
                return userCheck;
            }
        }

        var updated = await persistenceTaskClient.UpdateAsync(taskItem, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken cancellationToken)
    {
        var deleted = await persistenceTaskClient.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Kullanıcı geçerliyse <c>null</c>, değilse döndürülecek hata sonucunu verir.</summary>
    private async Task<ActionResult?> ValidateUserAsync(int userId, CancellationToken cancellationToken)
    {
        bool exists;

        try
        {
            exists = await userServiceClient.UserExistsAsync(userId, cancellationToken);
        }
        catch (UserServiceUnavailableException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "UserService is unavailable.",
                detail: "The task could not be saved because the assigned user could not be verified. Please try again later.");
        }

        if (exists)
        {
            return null;
        }

        ModelState.AddModelError(nameof(TaskItem.UserId), $"User with id {userId} does not exist.");
        return ValidationProblem(ModelState);
    }
}
