using Microsoft.EntityFrameworkCore;
using Rira.Domain.Entities;

namespace Rira.Tests.Infrastructure.Persistence;

public class PersonMappingTests
{
    [Fact]
    public async Task Migration_RoundTripsIdentityAndValueObjects()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var person = SqliteTestDatabase.Person();
        await database.SeedAsync(person);

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        var stored = await context.Persons.SingleAsync();
        Assert.Equal(person.Id, stored.Id);
        Assert.Equal(person.FirstName, stored.FirstName);
        Assert.Equal(person.LastName, stored.LastName);
        Assert.Equal("0123456789", stored.NationalCode.Value);
        Assert.Equal(person.BirthDate, stored.BirthDate);
        Assert.Empty(stored.DomainEvents);
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Null(context.Model.FindEntityType(typeof(Person))!.FindProperty("DomainEvents"));
    }

    [Fact]
    public async Task UniqueNationalCode_IsEnforcedByDatabase()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await database.SeedAsync(SqliteTestDatabase.Person());
        await using var context = database.CreateContext();
        context.Add(SqliteTestDatabase.Person());
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        await using var verify = database.CreateContext();
        Assert.Equal(1, await verify.Persons.CountAsync());
    }
}
