using Microsoft.AspNetCore.Mvc;
using TaskService.Interfaces;
using TaskService.Models;

namespace TaskService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TaskController(IPersistenceTaskClient persistenceTaskClient) : ControllerBase
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

        var updated = await persistenceTaskClient.UpdateAsync(taskItem, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken cancellationToken)
    {
        var deleted = await persistenceTaskClient.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}