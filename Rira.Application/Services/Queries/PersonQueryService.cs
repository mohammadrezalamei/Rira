using Rira.Application.Contracts.Persistence;
using Rira.Application.Contracts.Services;
using Rira.Application.DTOs.Persons;
using Rira.Domain.Common;
using Rira.Domain.ValueObjects;

namespace Rira.Application.Services.Queries;

public sealed class PersonQueryService : IPersonQueryService
{
    private readonly IPersonReadRepository _personReadRepository;

    public PersonQueryService(IPersonReadRepository personReadRepository)
    {
        ArgumentNullException.ThrowIfNull(personReadRepository);

        _personReadRepository = personReadRepository;
    }

    public async Task<Result<PersonResponseDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return Result<PersonResponseDto>.Failure(
                "شناسه شخص نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        var person = await _personReadRepository.GetByIdAsync(
            id,
            cancellationToken);

        return person is null
            ? Result<PersonResponseDto>.Failure(
                "شخص موردنظر یافت نشد.",
                ErrorType.NotFound)
            : Result<PersonResponseDto>.Success(person);
    }

    public async Task<Result<PersonResponseDto>> GetByNationalCodeAsync(
        string nationalCode,
        CancellationToken cancellationToken = default)
    {
        var nationalCodeResult = NationalCode.Create(nationalCode);

        if (!nationalCodeResult.IsSuccess)
            return Result<PersonResponseDto>.Failure(
                nationalCodeResult.Message!,
                nationalCodeResult.ErrorType);

        var person = await _personReadRepository.GetByNationalCodeAsync(
            nationalCodeResult.Value!.Value,
            cancellationToken);

        return person is null
            ? Result<PersonResponseDto>.Failure(
                "شخص موردنظر یافت نشد.",
                ErrorType.NotFound)
            : Result<PersonResponseDto>.Success(person);
    }

    public async Task<Result<IReadOnlyList<PersonResponseDto>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var persons = await _personReadRepository.GetAllAsync(
            cancellationToken);

        return Result<IReadOnlyList<PersonResponseDto>>.Success(persons);
    }
}
