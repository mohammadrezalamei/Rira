using Rira.Application.DTOs.Persons;

namespace Rira.Application.Contracts.Persistence;

public interface IPersonReadRepository
{
    Task<PersonResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PersonResponseDto?> GetByNationalCodeAsync(
        string nationalCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonResponseDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
