using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Rira.Domain.Entities;
using Rira.Domain.Repositories;
using Rira.Domain.ValueObjects;
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
    public async Task SequentialSaves_AfterBothPrechecksPass_RejectDuplicate()
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
    public async Task ParallelCreates_WithIndependentConnections_CommitOnlyOnePerson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"rira-race-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False;Default Timeout=10";
        try
        {
            await using var provider = BuildProvider(connectionString);
            await using (var setup = provider.CreateAsyncScope())
                await setup.ServiceProvider.GetRequiredService<RiraDbContext>().Database.MigrateAsync();

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var readyCount = 0;

            async Task<Result> CreateAsync()
            {
                await using var scope = provider.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<RiraDbContext>();
                var repository = new PersonRepository(context);
                var person = SqliteTestDatabase.Person();
                Assert.Null(await repository.GetByNationalCodeAsync(person.NationalCode));
                await repository.AddAsync(person);
                if (Interlocked.Increment(ref readyCount) == 2)
                    ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
                return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }

            // SQLite's async methods may execute synchronously; separate workers let
            // independent connections contend after both prechecks have completed.
            var results = await Task.WhenAll(Task.Run(CreateAsync), Task.Run(CreateAsync));
            Assert.Single(results, result => result.IsSuccess);
            var failure = Assert.Single(results, result => !result.IsSuccess);
            Assert.Equal(ErrorType.Conflict, failure.ErrorType);
            await using var verify = provider.CreateAsyncScope();
            Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<RiraDbContext>().Persons.CountAsync());
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task CommandUpdate_CancelledBeforeSave_DiscardsMutationAndReloadsForRetry()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitializeAsync();
        var person = SqliteTestDatabase.Person();
        await database.SeedAsync(person);
        await using var context = database.CreateContext();
        var repository = new PersonRepository(context);
        var unit = SqliteTestDatabase.UnitOfWork(context);
        using var cancellation = new CancellationTokenSource();
        var wrapper = new Mock<IPersonRepository>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetByIdAsync(person.Id, cancellation.Token))
            .Returns(() => repository.GetByIdAsync(person.Id));
        wrapper.Setup(x => x.GetByNationalCodeAsync(
                It.IsAny<NationalCode>(), person.Id, cancellation.Token))
            .ReturnsAsync(() =>
            {
                cancellation.Cancel();
                return (Person?)null;
            });
        var command = new PersonCommandService(wrapper.Object, unit);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.UpdateAsync(
            person.Id, new("Changed", "Name", "1234567891", new DateOnly(2000, 1, 1)), cancellation.Token));

        Assert.Empty(context.ChangeTracker.Entries());
        var reloaded = await repository.GetByIdAsync(person.Id);
        Assert.Equal("0123456789", reloaded!.NationalCode.Value);
        Assert.True((await new PersonCommandService(repository, unit).UpdateAsync(
            person.Id, new("Changed", "Name", "1234567891", new DateOnly(2000, 1, 1)))).IsSuccess);
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
