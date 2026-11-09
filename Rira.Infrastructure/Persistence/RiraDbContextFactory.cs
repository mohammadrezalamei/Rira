using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rira.Infrastructure.Persistence;

public sealed class RiraDbContextFactory : IDesignTimeDbContextFactory<RiraDbContext>
{
    public RiraDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : "Data Source=rira.db";
        return new RiraDbContext(new DbContextOptionsBuilder<RiraDbContext>()
            .UseSqlite(connectionString).Options);
    }
}
