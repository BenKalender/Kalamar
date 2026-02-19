using Microsoft.EntityFrameworkCore;
using PersistenceService.Models;

namespace PersistenceService.Data;

public class PersistenceDbContext(DbContextOptions<PersistenceDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
}