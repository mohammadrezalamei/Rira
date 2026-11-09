using Rira.Domain.Entities;
using Rira.Domain.ValueObjects;

namespace Rira.Domain.Repositories;

public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Person?> GetByNationalCodeAsync(
        NationalCode nationalCode,
        Guid? excludedPersonId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Person person,
        CancellationToken cancellationToken = default);

    void Update(Person person);

    void Remove(Person person);
}
