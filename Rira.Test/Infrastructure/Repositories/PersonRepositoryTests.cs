using Microsoft.EntityFrameworkCore;
using Rira.Domain.ValueObjects;
using Rira.Infrastructure.Repositories.Commands;

namespace Rira.Tests.Infrastructure.Repositories;

public class PersonRepositoryTests
{
    [Fact]
    public async Task AddUpdateDelete_ArePersistedOnlyWhenSaved()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        var repository = new PersonRepository(context);
        var unit = SqliteTestDatabase.UnitOfWork(context);
        var person = SqliteTestDatabase.Person();
        await repository.AddAsync(person);
        await using var observer = database.CreateContext();
        Assert.Equal(0, await observer.Persons.CountAsync());
        Assert.True((await unit.SaveChangesAsync()).IsSuccess);
        context.ChangeTracker.Clear();
        var loaded = await repository.GetByIdAsync(person.Id);
        Assert.NotNull(loaded);
        Assert.Equal(EntityState.Unchanged, context.Entry(loaded!).State);
        Assert.True(loaded!.Update("New", "Name", "1234567891", new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1)).IsSuccess);
        repository.Update(loaded);
        Assert.Equal("علی", (await observer.Persons.AsNoTracking().SingleAsync()).FirstName);
        Assert.True((await unit.SaveChangesAsync()).IsSuccess);
        Assert.Equal("New", (await observer.Persons.AsNoTracking().SingleAsync()).FirstName);
        repository.Remove(loaded);
        Assert.Equal(1, await observer.Persons.CountAsync());
        Assert.True((await unit.SaveChangesAsync()).IsSuccess);
        Assert.Null(await repository.GetByIdAsync(person.Id));
        Assert.Equal(0, await observer.Persons.CountAsync());
    }

    [Fact]
    public async Task Lookups_ReturnNullOrPersonAndRespectExcludedId()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var person = SqliteTestDatabase.Person();
        await database.SeedAsync(person);
        await using var context = database.CreateContext();
        var repository = new PersonRepository(context);
        var nationalCode = NationalCode.Create("۰۱۲۳۴۵۶۷۸۹").Value!;
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
        Assert.Equal(person.Id, (await repository.GetByNationalCodeAsync(nationalCode))!.Id);
        Assert.Null(await repository.GetByNationalCodeAsync(nationalCode, person.Id));
        Assert.NotNull(await repository.GetByNationalCodeAsync(nationalCode, Guid.NewGuid()));
        Assert.Null(await repository.GetByNationalCodeAsync(NationalCode.Create("1234567891").Value!));
    }

    [Fact]
    public async Task DetachedChanges_AreRejectedToPreserveOriginalConcurrencyValues()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        var repository = new PersonRepository(context);
        Assert.Throws<InvalidOperationException>(() => repository.Update(SqliteTestDatabase.Person()));
        Assert.Throws<InvalidOperationException>(() => repository.Remove(SqliteTestDatabase.Person()));
    }

    [Fact]
    public async Task CancelledRead_PropagatesCancellation()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var context = database.CreateContext();
        var repository = new PersonRepository(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetByIdAsync(Guid.NewGuid(), cancellation.Token));
    }
}
