using System;
using System.Collections.Generic;
using Rira.Domain.Common;
using Rira.Domain.Entities;
using Xunit;

namespace Rira.Tests.Domain.Entities;

public class PersonTests
{
    private const string ValidNationalCode = "0123456789";
    private const string AnotherValidNationalCode = "1234567891";

    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DateOnly ValidBirthDate = new(1995, 5, 20);

    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.NotNull(result.Value);

        var person = result.Value!;

        Assert.NotEqual(Guid.Empty, person.Id);
        Assert.Equal("علی", person.FirstName);
        Assert.Equal("احمدی", person.LastName);
        Assert.Equal(ValidNationalCode, person.NationalCode.Value);
        Assert.Equal(ValidBirthDate, person.BirthDate.Value);
    }

    [Fact]
    public void Create_WithSameData_ShouldGenerateDifferentIds()
    {
        var first = CreateValidPerson();
        var second = CreateValidPerson();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Create_WithEmptyFirstName_ShouldReturnValidationFailure(
        string? firstName)
    {
        var result = Person.Create(
            firstName,
            "احمدی",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        AssertCreateFailure(result, "نام نمی‌تواند خالی باشد.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Create_WithEmptyLastName_ShouldReturnValidationFailure(
        string? lastName)
    {
        var result = Person.Create(
            "علی",
            lastName,
            ValidNationalCode,
            ValidBirthDate,
            Today);

        AssertCreateFailure(
            result,
            "نام خانوادگی نمی‌تواند خالی باشد.");
    }

    [Fact]
    public void Create_WithFirstNameExceedingLimit_ShouldFail()
    {
        var firstName = new string('ا', Person.MaxFirstNameLength + 1);

        var result = Person.Create(
            firstName,
            "احمدی",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        AssertCreateFailure(
            result,
            $"نام نمی‌تواند بیشتر از {Person.MaxFirstNameLength} کاراکتر باشد.");
    }

    [Fact]
    public void Create_WithLastNameExceedingLimit_ShouldFail()
    {
        var lastName = new string('ا', Person.MaxLastNameLength + 1);

        var result = Person.Create(
            "علی",
            lastName,
            ValidNationalCode,
            ValidBirthDate,
            Today);

        AssertCreateFailure(
            result,
            $"نام خانوادگی نمی‌تواند بیشتر از {Person.MaxLastNameLength} کاراکتر باشد.");
    }

    [Fact]
    public void Create_WithNamesAtLengthLimits_ShouldSucceed()
    {
        var firstName = new string('ا', Person.MaxFirstNameLength);
        var lastName = new string('ب', Person.MaxLastNameLength);

        var result = Person.Create(
            firstName,
            lastName,
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(firstName, result.Value!.FirstName);
        Assert.Equal(lastName, result.Value.LastName);
    }

    [Fact]
    public void Create_ShouldValidateNameLengthsAfterTrimming()
    {
        var firstName = new string('ا', Person.MaxFirstNameLength);
        var lastName = new string('ب', Person.MaxLastNameLength);

        var result = Person.Create(
            $"  {firstName}  ",
            $"\t{lastName}\r\n",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(firstName, result.Value!.FirstName);
        Assert.Equal(lastName, result.Value.LastName);
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_ShouldTrimNames()
    {
        var result = Person.Create(
            "  علی  ",
            "\tاحمدی\r\n",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("علی", result.Value!.FirstName);
        Assert.Equal("احمدی", result.Value.LastName);
    }

    [Theory]
    [InlineData("علی رضا", "احمدی نیا")]
    [InlineData("علی‌رضا", "احمدی‌نیا")]
    [InlineData("Anne-Marie", "O'Neill")]
    public void Create_WithCompoundNames_ShouldPreserveTheirContent(
        string firstName,
        string lastName)
    {
        var result = Person.Create(
            firstName,
            lastName,
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(firstName, result.Value!.FirstName);
        Assert.Equal(lastName, result.Value.LastName);
    }

    [Theory]
    [InlineData(null, "کد ملی نمی‌تواند خالی باشد.")]
    [InlineData("", "کد ملی نمی‌تواند خالی باشد.")]
    [InlineData("123", "کد ملی باید دقیقاً ۱۰ رقم باشد.")]
    [InlineData("012345678A", "کد ملی فقط می‌تواند شامل رقم باشد.")]
    [InlineData(
        "1111111111",
        "کد ملی نمی‌تواند شامل ارقام کاملاً یکسان باشد.")]
    [InlineData("0123456780", "کد ملی واردشده معتبر نیست.")]
    public void Create_WithInvalidNationalCode_ShouldPropagateValidationFailure(
        string? nationalCode,
        string expectedMessage)
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            nationalCode,
            ValidBirthDate,
            Today);

        AssertCreateFailure(result, expectedMessage);
    }

    [Theory]
    [InlineData("۰۱۲۳۴۵۶۷۸۹")]
    [InlineData("٠١٢٣٤٥٦٧٨٩")]
    [InlineData(" 0123456789 ")]
    public void Create_WithNormalizableNationalCode_ShouldStoreNormalizedValue(
        string nationalCode)
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            nationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(
            ValidNationalCode,
            result.Value!.NationalCode.Value);
    }

    [Fact]
    public void Create_WithDefaultBirthDate_ShouldFail()
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            ValidNationalCode,
            default,
            Today);

        AssertCreateFailure(
            result,
            "تاریخ تولد باید مشخص شود.");
    }

    [Fact]
    public void Create_WithFutureBirthDate_ShouldFail()
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            ValidNationalCode,
            Today.AddDays(1),
            Today);

        AssertCreateFailure(
            result,
            "تاریخ تولد نمی‌تواند در آینده باشد.");
    }

    [Fact]
    public void Create_WithDefaultReferenceDate_ShouldFail()
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            ValidNationalCode,
            ValidBirthDate,
            default);

        AssertCreateFailure(
            result,
            "تاریخ مرجع اعتبارسنجی مشخص نشده است.");
    }

    [Fact]
    public void Create_WithBirthDateEqualToToday_ShouldSucceed()
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            ValidNationalCode,
            Today,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(Today, result.Value!.BirthDate.Value);
    }

    [Fact]
    public void Update_WithValidData_ShouldUpdateAllFieldsAndPreserveId()
    {
        var person = CreateValidPerson();
        var originalId = person.Id;
        var newBirthDate = new DateOnly(2000, 2, 29);

        var result = person.Update(
            "رضا",
            "محمدی",
            AnotherValidNationalCode,
            newBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);

        Assert.Equal(originalId, person.Id);
        Assert.Equal("رضا", person.FirstName);
        Assert.Equal("محمدی", person.LastName);
        Assert.Equal(
            AnotherValidNationalCode,
            person.NationalCode.Value);
        Assert.Equal(newBirthDate, person.BirthDate.Value);
    }

    [Fact]
    public void Update_WithNormalizableValues_ShouldNormalizeBeforeStoring()
    {
        var person = CreateValidPerson();

        var result = person.Update(
            "  رضا  ",
            "\tمحمدی\r\n",
            "  ۱۲۳۴۵۶۷۸۹۱  ",
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("رضا", person.FirstName);
        Assert.Equal("محمدی", person.LastName);
        Assert.Equal(
            AnotherValidNationalCode,
            person.NationalCode.Value);
    }

    [Fact]
    public void Update_WithNamesAtLengthLimits_ShouldSucceed()
    {
        var person = CreateValidPerson();
        var firstName = new string('ا', Person.MaxFirstNameLength);
        var lastName = new string('ب', Person.MaxLastNameLength);

        var result = person.Update(
            $" {firstName} ",
            $" {lastName} ",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(firstName, person.FirstName);
        Assert.Equal(lastName, person.LastName);
    }

    [Fact]
    public void Update_WithBirthDateEqualToToday_ShouldSucceed()
    {
        var person = CreateValidPerson();

        var result = person.Update(
            person.FirstName,
            person.LastName,
            person.NationalCode.Value,
            Today,
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, person.BirthDate.Value);
    }

    [Fact]
    public void Update_WithUnchangedData_ShouldSucceedAndPreserveValues()
    {
        var person = CreateValidPerson();
        var originalId = person.Id;

        var result = person.Update(
            person.FirstName,
            person.LastName,
            person.NationalCode.Value,
            person.BirthDate.Value,
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalId, person.Id);
        Assert.Equal("علی", person.FirstName);
        Assert.Equal("احمدی", person.LastName);
        Assert.Equal(ValidNationalCode, person.NationalCode.Value);
        Assert.Equal(ValidBirthDate, person.BirthDate.Value);
    }

    [Theory]
    [MemberData(nameof(InvalidUpdateCases))]
    public void Update_WithInvalidData_ShouldFailWithoutChangingAnyField(
        string? firstName,
        string? lastName,
        string? nationalCode,
        DateOnly birthDate,
        DateOnly today,
        string expectedMessage)
    {
        var person = CreateValidPerson();

        var originalId = person.Id;
        var originalFirstName = person.FirstName;
        var originalLastName = person.LastName;
        var originalNationalCode = person.NationalCode;
        var originalBirthDate = person.BirthDate;

        var result = person.Update(
            firstName,
            lastName,
            nationalCode,
            birthDate,
            today);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(expectedMessage, result.Message);

        Assert.Equal(originalId, person.Id);
        Assert.Equal(originalFirstName, person.FirstName);
        Assert.Equal(originalLastName, person.LastName);

        Assert.Same(originalNationalCode, person.NationalCode);
        Assert.Same(originalBirthDate, person.BirthDate);
    }

    public static IEnumerable<object?[]> InvalidUpdateCases()
    {
        var newBirthDate = new DateOnly(2000, 1, 1);

        foreach (var invalidName in new string?[]
                 {
                     null,
                     "",
                     " ",
                     "\t\r\n"
                 })
        {
            yield return new object?[]
            {
                invalidName,
                "محمدی",
                AnotherValidNationalCode,
                newBirthDate,
                Today,
                "نام نمی‌تواند خالی باشد."
            };

            yield return new object?[]
            {
                "رضا",
                invalidName,
                AnotherValidNationalCode,
                newBirthDate,
                Today,
                "نام خانوادگی نمی‌تواند خالی باشد."
            };
        }

        yield return new object?[]
        {
            new string('ا', Person.MaxFirstNameLength + 1),
            "محمدی",
            AnotherValidNationalCode,
            newBirthDate,
            Today,
            $"نام نمی‌تواند بیشتر از {Person.MaxFirstNameLength} کاراکتر باشد."
        };

        yield return new object?[]
        {
            "رضا",
            new string('ا', Person.MaxLastNameLength + 1),
            AnotherValidNationalCode,
            newBirthDate,
            Today,
            $"نام خانوادگی نمی‌تواند بیشتر از {Person.MaxLastNameLength} کاراکتر باشد."
        };

        foreach (var invalidNationalCode in new string?[]
                 {
                     null,
                     "",
                     " "
                 })
        {
            yield return new object?[]
            {
                "رضا",
                "محمدی",
                invalidNationalCode,
                newBirthDate,
                Today,
                "کد ملی نمی‌تواند خالی باشد."
            };
        }

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            "123",
            newBirthDate,
            Today,
            "کد ملی باید دقیقاً ۱۰ رقم باشد."
        };

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            "012345678A",
            newBirthDate,
            Today,
            "کد ملی فقط می‌تواند شامل رقم باشد."
        };

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            "1111111111",
            newBirthDate,
            Today,
            "کد ملی نمی‌تواند شامل ارقام کاملاً یکسان باشد."
        };

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            "0123456780",
            newBirthDate,
            Today,
            "کد ملی واردشده معتبر نیست."
        };

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            AnotherValidNationalCode,
            default(DateOnly),
            Today,
            "تاریخ تولد باید مشخص شود."
        };

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            AnotherValidNationalCode,
            Today.AddDays(1),
            Today,
            "تاریخ تولد نمی‌تواند در آینده باشد."
        };

        yield return new object?[]
        {
            "رضا",
            "محمدی",
            AnotherValidNationalCode,
            newBirthDate,
            default(DateOnly),
            "تاریخ مرجع اعتبارسنجی مشخص نشده است."
        };
    }

    private static Person CreateValidPerson()
    {
        var result = Person.Create(
            "علی",
            "احمدی",
            ValidNationalCode,
            ValidBirthDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        return result.Value!;
    }

    private static void AssertCreateFailure(
        Result<Person> result,
        string expectedMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Value);
    }
}
