using Microsoft.AspNetCore.Mvc;
using UserService.Interfaces;
using UserService.Models;

namespace UserService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController(IPersistenceUserClient persistenceUserClient) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<User>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await persistenceUserClient.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<User>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await persistenceUserClient.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<User>> AddUser(User user, CancellationToken cancellationToken)
    {
        var createdUser = await persistenceUserClient.CreateAsync(user, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, User user, CancellationToken cancellationToken)
    {
        if (id != user.Id)
        {
            return BadRequest();
        }

        var updated = await persistenceUserClient.UpdateAsync(user, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var deleted = await persistenceUserClient.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
