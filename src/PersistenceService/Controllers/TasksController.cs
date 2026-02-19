using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersistenceService.Data;
using PersistenceService.Models;

namespace PersistenceService.Controllers;

[Route("api/tasks")]
[ApiController]
public class TasksController(PersistenceDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItem>>> GetAll(CancellationToken cancellationToken)
    {
        var tasks = await dbContext.Tasks.AsNoTracking().ToListAsync(cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var taskItem = await dbContext.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return taskItem is null ? NotFound() : Ok(taskItem);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create(TaskItem taskItem, CancellationToken cancellationToken)
    {
        dbContext.Tasks.Add(taskItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = taskItem.Id }, taskItem);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TaskItem taskItem, CancellationToken cancellationToken)
    {
        if (id != taskItem.Id)
        {
            return BadRequest();
        }

        var exists = await dbContext.Tasks.AnyAsync(x => x.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        dbContext.Tasks.Update(taskItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var taskItem = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (taskItem is null)
        {
            return NotFound();
        }

        dbContext.Tasks.Remove(taskItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}