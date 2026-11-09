using Microsoft.EntityFrameworkCore;
using Rira.Domain.Entities;

namespace Rira.Infrastructure.Persistence;

public sealed class RiraDbContext(DbContextOptions<RiraDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RiraDbContext).Assembly);
    }
}
