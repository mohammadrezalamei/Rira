using Microsoft.EntityFrameworkCore;
using Rira.Domain.Entities;
using Rira.Domain.Repositories;
using Rira.Domain.ValueObjects;
using Rira.Infrastructure.Persistence;

namespace Rira.Infrastructure.Repositories.Commands;

public sealed class PersonRepository(RiraDbContext context) : IPersonRepository
{
    public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Persons.SingleOrDefaultAsync(person => person.Id == id, cancellationToken);

    public Task<Person?> GetByNationalCodeAsync(NationalCode nationalCode,
        Guid? excludedPersonId = null, CancellationToken cancellationToken = default)
        => context.Persons.SingleOrDefaultAsync(person => person.NationalCode == nationalCode &&
            (!excludedPersonId.HasValue || person.Id != excludedPersonId.Value), cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        await context.Persons.AddAsync(person, cancellationToken);
    }

    public void Update(Person person)
    {
        if (context.Entry(person).State == EntityState.Detached)
            throw new InvalidOperationException("Load the person through this unit of work before updating it.");
        context.ChangeTracker.DetectChanges();
    }

    public void Remove(Person person)
    {
        if (context.Entry(person).State == EntityState.Detached)
            throw new InvalidOperationException("Load the person through this unit of work before deleting it.");
        context.Persons.Remove(person);
    }
}
