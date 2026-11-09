using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rira.Application.Contracts.Common;
using Rira.Application.Contracts.Persistence;
using Rira.Application.DTOs.Persons;
using Rira.Application.Services.Commands;
using Rira.Application.Services.Queries;
using Rira.Domain.Common;
using Rira.Infrastructure;
using Rira.Infrastructure.Persistence;
using Rira.Infrastructure.Repositories.Commands;

namespace Rira.Tests.Infrastructure;

public class PersonPersistenceIntegrationTests
{
    [Fact]
    public async Task RegisteredServices_ShareScopedContextAndPersistAcrossProviderRestarts()
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"rira-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            Guid id;
            await using (var provider = BuildProvider(connectionString))
            {
                await using var scope = provider.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<RiraDbContext>();
                Assert.Same(context, scope.ServiceProvider.GetRequiredService<RiraDbContext>());
                await context.Database.MigrateAsync();
                var service = scope.ServiceProvider.GetRequiredService<PersonCommandService>();
                var result = await service.CreateAsync(new("علی", "احمدی", "۰۱۲۳۴۵۶۷۸۹", new DateOnly(1995, 5, 20)));
                Assert.True(result.IsSuccess);
                id = result.Value!.Id;
            }

            await using (var provider = BuildProvider(connectionString))
            {
                await using var scope = provider.CreateAsyncScope();
                var query = scope.ServiceProvider.GetRequiredService<PersonQueryService>();
                var found = await query.GetByIdAsync(id);
                Assert.True(found.IsSuccess);
                Assert.Equal("0123456789", found.Value!.NationalCode);
                var context = scope.ServiceProvider.GetRequiredService<RiraDbContext>();
                Assert.Empty(context.ChangeTracker.Entries());
                var command = scope.ServiceProvider.GetRequiredService<PersonCommandService>();
                var updated = await command.UpdateAsync(id, new("رضا", "محمدی", "1234567891", new DateOnly(2000, 1, 1)));
                Assert.True(updated.IsSuccess);
                Assert.Equal("رضا", (await query.GetByIdAsync(id)).Value!.FirstName);
                Assert.True((await command.DeleteAsync(id)).IsSuccess);
                Assert.Equal(ErrorType.NotFound, (await query.GetByIdAsync(id)).ErrorType);
            }
        }
        finally
        {
            // Only this test's uniquely named database and SQLite sidecars are removed.
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task CompetingCreates_AfterBothPrechecksPass_OnlyOneCanCommit()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstRepository = new PersonRepository(first);
        var secondRepository = new PersonRepository(second);
        var person = SqliteTestDatabase.Person();
        Assert.Null(await firstRepository.GetByNationalCodeAsync(person.NationalCode));
        Assert.Null(await secondRepository.GetByNationalCodeAsync(person.NationalCode));
        await firstRepository.AddAsync(person);
        await secondRepository.AddAsync(SqliteTestDatabase.Person());
        Assert.True((await SqliteTestDatabase.UnitOfWork(first).SaveChangesAsync()).IsSuccess);
        Assert.Equal(ErrorType.Conflict, (await SqliteTestDatabase.UnitOfWork(second).SaveChangesAsync()).ErrorType);
        await using var verify = database.CreateContext();
        Assert.Equal(1, await verify.Persons.CountAsync());
    }

    [Fact]
    public async Task CommandUpdate_SaveConflict_RestoresEntityAndCanBeRetried()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var person = SqliteTestDatabase.Person();
        await database.SeedAsync(person);
        await using var context = database.CreateContext();
        var tracked = await context.Persons.SingleAsync();
        // Stage a competing insert in the same unit of work to exercise rollback
        // after domain mutation rather than the service's early duplicate check.
        context.Add(SqliteTestDatabase.Person("1234567891"));
        var command = new PersonCommandService(new PersonRepository(context), SqliteTestDatabase.UnitOfWork(context));
        var result = await command.UpdateAsync(person.Id,
            new("Changed", "Name", "1234567891", new DateOnly(2000, 1, 1)));
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("علی", tracked.FirstName);
        Assert.Equal("0123456789", tracked.NationalCode.Value);
        Assert.Empty(context.ChangeTracker.Entries());
        await using var verify = database.CreateContext();
        Assert.Equal("0123456789", (await verify.Persons.SingleAsync()).NationalCode.Value);
        var retry = await command.UpdateAsync(person.Id,
            new("Changed", "Name", "1234567891", new DateOnly(2000, 1, 1)));
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public void Registration_RejectsEmptyConnectionString()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddInfrastructure(" "));
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString);
        services.AddScoped<PersonCommandService>();
        services.AddScoped<PersonQueryService>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
