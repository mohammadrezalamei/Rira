using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rira.Domain.Entities;
using Rira.Infrastructure.Persistence;

namespace Rira.Tests.Infrastructure;

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public RiraDbContext CreateContext() => new(new DbContextOptionsBuilder<RiraDbContext>()
        .UseSqlite(_connection).Options);

    public static UnitOfWork UnitOfWork(RiraDbContext context)
        => new(context, NullLogger<UnitOfWork>.Instance);

    public static Person Person(string nationalCode = "0123456789")
        => Rira.Domain.Entities.Person.Create("علی", "احمدی", nationalCode,
            new DateOnly(1995, 5, 20), new DateOnly(2026, 1, 1)).Value!;

    public async Task SeedAsync(params Person[] persons)
    {
        await using var context = CreateContext();
        context.AddRange(persons);
        await context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
