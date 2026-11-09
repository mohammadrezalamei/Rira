using Microsoft.EntityFrameworkCore;
using Rira.Domain.Common;

namespace Rira.Tests.Infrastructure.Persistence;

public class UnitOfWorkTests
{
    [Fact]
    public async Task DiscardChanges_RestoresModifiedDeletedAndDetachesAddedEntities()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var first = SqliteTestDatabase.Person();
        var second = SqliteTestDatabase.Person("1234567891");
        await database.SeedAsync(first, second);
        await using var context = database.CreateContext();
        var modified = await context.Persons.SingleAsync(p => p.Id == first.Id);
        var deleted = await context.Persons.SingleAsync(p => p.Id == second.Id);
        modified.Update("Changed", "Name", "0123456789", modified.BirthDate.Value, new DateOnly(2026, 1, 1));
        context.Remove(deleted);
        context.Add(SqliteTestDatabase.Person("0000000140"));

        var unit = SqliteTestDatabase.UnitOfWork(context);
        unit.DiscardChanges();
        unit.DiscardChanges();
        Assert.Equal("علی", modified.FirstName);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.True((await unit.SaveChangesAsync()).IsSuccess);
        await using var verify = database.CreateContext();
        Assert.Equal(2, await verify.Persons.CountAsync());
        Assert.All(await verify.Persons.ToListAsync(), p => Assert.Equal("علی", p.FirstName));
    }

    [Fact]
    public async Task DuplicateSave_RollsBackWholeBatchAndAllowsReuse()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var first = SqliteTestDatabase.Person();
        await database.SeedAsync(first);
        await using var context = database.CreateContext();
        var tracked = await context.Persons.SingleAsync();
        tracked.Update("Changed", "Name", "0123456789", tracked.BirthDate.Value, new DateOnly(2026, 1, 1));
        context.Add(SqliteTestDatabase.Person("1234567891"));
        context.Add(SqliteTestDatabase.Person());
        var unit = SqliteTestDatabase.UnitOfWork(context);
        var result = await unit.SaveChangesAsync();
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("علی", tracked.FirstName);
        Assert.Empty(context.ChangeTracker.Entries());
        await using (var verify = database.CreateContext())
        {
            Assert.Equal("علی", (await verify.Persons.SingleAsync()).FirstName);
        }
        context.Add(SqliteTestDatabase.Person("1234567891"));
        Assert.True((await unit.SaveChangesAsync()).IsSuccess);
        Assert.Equal(2, await context.Persons.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleUpdateOrDelete_ReturnsConflictWithoutOverwritingWinner(bool delete)
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await database.SeedAsync(SqliteTestDatabase.Person());
        await using var winner = database.CreateContext();
        await using var loser = database.CreateContext();
        var first = await winner.Persons.SingleAsync();
        var stale = await loser.Persons.SingleAsync();
        first.Update("Winner", "Name", "0123456789", first.BirthDate.Value, new DateOnly(2026, 1, 1));
        Assert.True((await SqliteTestDatabase.UnitOfWork(winner).SaveChangesAsync()).IsSuccess);
        if (delete)
            loser.Remove(stale);
        else
            stale.Update("Loser", "Name", "0123456789", stale.BirthDate.Value, new DateOnly(2026, 1, 1));
        var result = await SqliteTestDatabase.UnitOfWork(loser).SaveChangesAsync();
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Empty(loser.ChangeTracker.Entries());
        Assert.Equal("Winner", (await loser.Persons.SingleAsync()).FirstName);
    }

    [Fact]
    public async Task CancelledSave_DiscardsChangesAndPropagatesCancellation()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        context.Add(SqliteTestDatabase.Person());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SqliteTestDatabase.UnitOfWork(context).SaveChangesAsync(cancellation.Token));
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(await context.Persons.ToListAsync());
    }

    [Fact]
    public async Task DatabaseFailure_ReturnsGenericErrorAndDiscardsChanges()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DROP TABLE Persons");
        context.Add(SqliteTestDatabase.Person());
        var result = await SqliteTestDatabase.UnitOfWork(context).SaveChangesAsync();
        Assert.Equal(ErrorType.ServerError, result.ErrorType);
        Assert.Equal("ذخیره اطلاعات با خطا مواجه شد.", result.Message);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
