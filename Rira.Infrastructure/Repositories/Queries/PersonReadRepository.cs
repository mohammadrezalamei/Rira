using Microsoft.EntityFrameworkCore;
using Rira.Application.Contracts.Persistence;
using Rira.Application.DTOs.Persons;
using Rira.Infrastructure.Persistence;

namespace Rira.Infrastructure.Repositories.Queries;

public sealed class PersonReadRepository(RiraDbContext context) : IPersonReadRepository
{
    // Unmapped DTO queries avoid materializing or tracking domain aggregates.
    private IQueryable<PersonResponseDto> Persons => context.Database.SqlQuery<PersonResponseDto>(
        $"SELECT Id, FirstName, LastName, NationalCode, BirthDate FROM Persons");

    public Task<PersonResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Persons.SingleOrDefaultAsync(person => person.Id == id, cancellationToken);

    public Task<PersonResponseDto?> GetByNationalCodeAsync(string nationalCode, CancellationToken cancellationToken = default)
        => Persons.SingleOrDefaultAsync(person => person.NationalCode == nationalCode, cancellationToken);

    public async Task<IReadOnlyList<PersonResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Persons.OrderBy(person => person.Id).ToListAsync(cancellationToken);
}
