using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rira.Infrastructure.Repositories.Queries;

namespace Rira.Tests.Infrastructure.Repositories;

public class PersonReadRepositoryTests
{
    [Fact]
    public async Task Queries_ReturnDtosWithoutTrackingAndPreserveLeadingZeros()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var first = SqliteTestDatabase.Person();
        var second = SqliteTestDatabase.Person("1234567891");
        await database.SeedAsync(first, second);
        await using var context = database.CreateContext();
        var repository = new PersonReadRepository(context);
        var byId = await repository.GetByIdAsync(first.Id);
        Assert.NotNull(byId);
        Assert.Equal(first.FirstName, byId!.FirstName);
        Assert.Equal(first.LastName, byId.LastName);
        Assert.Equal("0123456789", byId.NationalCode);
        Assert.Equal(first.BirthDate.Value, byId.BirthDate);
        Assert.Equal(byId, await repository.GetByNationalCodeAsync("0123456789"));
        Assert.Equal(2, (await repository.GetAllAsync()).Count);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MissingRows_ReturnNullAndEmptyList()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        var repository = new PersonReadRepository(context);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await repository.GetByNationalCodeAsync("0123456789"));
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task Query_ReadsPersistedDataInsteadOfUnsavedTrackedEntity()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await database.SeedAsync(SqliteTestDatabase.Person());
        await using var context = database.CreateContext();
        var tracked = await context.Persons.SingleAsync();
        tracked.Update("Unsaved", "Name", "0123456789", tracked.BirthDate.Value, new DateOnly(2026, 1, 1));
        var response = await new PersonReadRepository(context).GetByIdAsync(tracked.Id);
        Assert.Equal("علی", response!.FirstName);
        Assert.Single(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task NationalCodeFilter_IsParameterized()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await database.SeedAsync(SqliteTestDatabase.Person());
        await using var context = database.CreateContext();
        var repository = new PersonReadRepository(context);
        Assert.Null(await repository.GetByNationalCodeAsync("' OR 1=1 --"));
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task QueryFailures_AreNotConvertedToEmptyResults()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        var repository = new PersonReadRepository(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAllAsync(cancellation.Token));
        await context.Database.ExecuteSqlRawAsync("DROP TABLE Persons");
        await Assert.ThrowsAsync<SqliteException>(() => repository.GetAllAsync());
    }
}
