using Rira.Application.DTOs.Persons;
using Rira.Domain.Common;

namespace Rira.Application.Contracts.Services;

public interface IPersonQueryService
{
    Task<Result<PersonResponseDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<PersonResponseDto>> GetByNationalCodeAsync(
        string nationalCode,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PersonResponseDto>>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
