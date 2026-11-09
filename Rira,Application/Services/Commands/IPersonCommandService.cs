using Rira.Application.DTOs.Persons;
using Rira.Domain.Common;

namespace Rira.Application.Contracts.Services;

public interface IPersonCommandService
{
    Task<Result<PersonResponseDto>> CreateAsync(
        CreatePersonRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result<PersonResponseDto>> UpdateAsync(
        Guid id,
        UpdatePersonRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
