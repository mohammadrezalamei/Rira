using Rira.Application.Contracts.Common;
using Rira.Application.Contracts.Services;
using Rira.Application.DTOs.Persons;
using Rira.Domain.Common;
using Rira.Domain.Entities;
using Rira.Domain.Repositories;
using Rira.Domain.ValueObjects;

namespace Rira.Application.Services.Commands;

public sealed class PersonCommandService : IPersonCommandService
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PersonCommandService(
        IPersonRepository personRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(personRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _personRepository = personRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PersonResponseDto>> CreateAsync(
        CreatePersonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return Result<PersonResponseDto>.Failure(
                "اطلاعات شخص نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        var personResult = Person.Create(
            request.FirstName,
            request.LastName,
            request.NationalCode,
            request.BirthDate,
            GetToday());

        if (!personResult.IsSuccess)
            return Result<PersonResponseDto>.Failure(
                personResult.Message!,
                personResult.ErrorType);

        var person = personResult.Value!;

        var existingPerson =
            await _personRepository.GetByNationalCodeAsync(
                person.NationalCode,
                cancellationToken: cancellationToken);

        if (existingPerson is not null)
            return Result<PersonResponseDto>.Failure(
                "شخصی با این کد ملی قبلاً ثبت شده است.",
                ErrorType.Conflict);

        var saveResult = await PersistAsync(
            () => _personRepository.AddAsync(person, cancellationToken),
            cancellationToken);

        if (!saveResult.IsSuccess)
            return Result<PersonResponseDto>.Failure(
                saveResult.Message!,
                saveResult.ErrorType);

        return Result<PersonResponseDto>.Success(
            MapToResponse(person));
    }

    public async Task<Result<PersonResponseDto>> UpdateAsync(
        Guid id,
        UpdatePersonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return Result<PersonResponseDto>.Failure(
                "شناسه شخص نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        if (request is null)
            return Result<PersonResponseDto>.Failure(
                "اطلاعات شخص نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        var person = await _personRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (person is null)
            return Result<PersonResponseDto>.Failure(
                "شخص موردنظر یافت نشد.",
                ErrorType.NotFound);

        var nationalCodeResult = NationalCode.Create(
            request.NationalCode);

        if (!nationalCodeResult.IsSuccess)
            return Result<PersonResponseDto>.Failure(
                nationalCodeResult.Message!,
                nationalCodeResult.ErrorType);

        var existingPerson =
            await _personRepository.GetByNationalCodeAsync(
                nationalCodeResult.Value!,
                excludedPersonId: id,
                cancellationToken: cancellationToken);

        if (existingPerson is not null)
            return Result<PersonResponseDto>.Failure(
                "شخصی با این کد ملی قبلاً ثبت شده است.",
                ErrorType.Conflict);

        var updateResult = person.Update(
            request.FirstName,
            request.LastName,
            nationalCodeResult.Value!.Value,
            request.BirthDate,
            GetToday());

        if (!updateResult.IsSuccess)
            return Result<PersonResponseDto>.Failure(
                updateResult.Message!,
                updateResult.ErrorType);

        var saveResult = await PersistAsync(
            () =>
            {
                _personRepository.Update(person);
                return Task.CompletedTask;
            },
            cancellationToken);

        if (!saveResult.IsSuccess)
            return Result<PersonResponseDto>.Failure(
                saveResult.Message!,
                saveResult.ErrorType);

        return Result<PersonResponseDto>.Success(
            MapToResponse(person));
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return Result.Failure(
                "شناسه شخص نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        var person = await _personRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (person is null)
            return Result.Failure(
                "شخص موردنظر یافت نشد.",
                ErrorType.NotFound);

        return await PersistAsync(
            () =>
            {
                _personRepository.Remove(person);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    private async Task<Result> PersistAsync(
        Func<Task> stageChanges,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await stageChanges();
        }
        catch
        {
            // SaveChangesAsync owns cleanup once saving starts.
            _unitOfWork.DiscardChanges();
            throw;
        }

        return await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static DateOnly GetToday()
    {
        var localNow = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            BusinessTimeZone);

        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private static PersonResponseDto MapToResponse(Person person)
    {
        return new PersonResponseDto(
            person.Id,
            person.FirstName,
            person.LastName,
            person.NationalCode.Value,
            person.BirthDate.Value);
    }
}
