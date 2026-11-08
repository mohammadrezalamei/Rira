using Rira.Domain.Common;
using Rira.Domain.ValueObjects;
using BirthDateValue = Rira.Domain.ValueObjects.BirthDate;
using NationalCodeValue = Rira.Domain.ValueObjects.NationalCode;

namespace Rira.Domain.Entities;

public sealed class Person : AggregateRoot<Guid>
{
    public const int MaxFirstNameLength = 100;
    public const int MaxLastNameLength = 100;

    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public NationalCode NationalCode { get; private set; }
    public BirthDate BirthDate { get; private set; }

    private Person(
        string firstName,
        string lastName,
        NationalCode nationalCode,
        BirthDate birthDate)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        LastName = lastName;
        NationalCode = nationalCode;
        BirthDate = birthDate;
    }

    public static Result<Person> Create(
        string? firstName,
        string? lastName,
        string? nationalCode,
        DateOnly birthDate,
        DateOnly today)
    {
        var nameValidation = ValidateNames(firstName, lastName);

        if (!nameValidation.IsSuccess)
            return Result<Person>.Failure(
                nameValidation.Message!,
                nameValidation.ErrorType);

        var nationalCodeResult = NationalCodeValue.Create(nationalCode);

        if (!nationalCodeResult.IsSuccess)
            return Result<Person>.Failure(
                nationalCodeResult.Message!,
                nationalCodeResult.ErrorType);

        var birthDateResult = BirthDateValue.Create(birthDate, today);

        if (!birthDateResult.IsSuccess)
            return Result<Person>.Failure(
                birthDateResult.Message!,
                birthDateResult.ErrorType);

        var person = new Person(
            firstName!.Trim(),
            lastName!.Trim(),
            nationalCodeResult.Value!,
            birthDateResult.Value!);

        return Result<Person>.Success(person);
    }

    public Result Update(
        string? firstName,
        string? lastName,
        string? nationalCode,
        DateOnly birthDate,
        DateOnly today)
    {
        var nameValidation = ValidateNames(firstName, lastName);

        if (!nameValidation.IsSuccess)
            return nameValidation;

        var nationalCodeResult = NationalCodeValue.Create(nationalCode);

        if (!nationalCodeResult.IsSuccess)
            return Result.Failure(
                nationalCodeResult.Message!,
                nationalCodeResult.ErrorType);

        var birthDateResult = BirthDateValue.Create(birthDate, today);

        if (!birthDateResult.IsSuccess)
            return Result.Failure(
                birthDateResult.Message!,
                birthDateResult.ErrorType);

        FirstName = firstName!.Trim();
        LastName = lastName!.Trim();
        NationalCode = nationalCodeResult.Value!;
        BirthDate = birthDateResult.Value!;

        return Result.Success();
    }

    private static Result ValidateNames(
        string? firstName,
        string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure(
                "نام نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        if (firstName.Trim().Length > MaxFirstNameLength)
            return Result.Failure(
                $"نام نمی‌تواند بیشتر از {MaxFirstNameLength} کاراکتر باشد.",
                ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure(
                "نام خانوادگی نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        if (lastName.Trim().Length > MaxLastNameLength)
            return Result.Failure(
                $"نام خانوادگی نمی‌تواند بیشتر از {MaxLastNameLength} کاراکتر باشد.",
                ErrorType.Validation);

        return Result.Success();
    }
}
