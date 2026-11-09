using System;
using System.Collections.Generic;
using System.Globalization;
using Rira.Domain.Common;
using Rira.Domain.ValueObjects;
using Xunit;

namespace Rira.Tests.Domain.ValueObjects;

public class BirthDateTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DateOnly ValidDate = new(1995, 5, 20);

    [Fact]
    public void Create_WithValidDate_ShouldReturnSuccess()
    {
        var result = BirthDate.Create(ValidDate, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(ValidDate, result.Value!.Value);
    }

    [Fact]
    public void Create_WithDefaultReferenceDate_ShouldReturnValidationFailure()
    {
        var result = BirthDate.Create(ValidDate, default);

        AssertValidationFailure(
            result,
            "تاریخ مرجع اعتبارسنجی مشخص نشده است.");
    }

    [Fact]
    public void Create_WithDefaultBirthDate_ShouldReturnValidationFailure()
    {
        var result = BirthDate.Create(default, Today);

        AssertValidationFailure(
            result,
            "تاریخ تولد باید مشخص شود.");
    }

    [Fact]
    public void Create_WithDateEqualToToday_ShouldSucceed()
    {
        var result = BirthDate.Create(Today, Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(Today, result.Value!.Value);
    }

    [Fact]
    public void Create_WithYesterday_ShouldSucceed()
    {
        var yesterday = Today.AddDays(-1);

        var result = BirthDate.Create(yesterday, Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(yesterday, result.Value!.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    public void Create_WithFutureDate_ShouldReturnValidationFailure(
        int daysInFuture)
    {
        var futureDate = Today.AddDays(daysInFuture);

        var result = BirthDate.Create(futureDate, Today);

        AssertValidationFailure(
            result,
            "تاریخ تولد نمی‌تواند در آینده باشد.");
    }

    [Fact]
    public void Create_WithMaxDateAndEarlierReferenceDate_ShouldFail()
    {
        var result = BirthDate.Create(DateOnly.MaxValue, Today);

        AssertValidationFailure(
            result,
            "تاریخ تولد نمی‌تواند در آینده باشد.");
    }

    [Fact]
    public void Create_WithLeapDayInPast_ShouldSucceed()
    {
        var leapDay = new DateOnly(2000, 2, 29);

        var result = BirthDate.Create(leapDay, Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(leapDay, result.Value!.Value);
    }

    [Fact]
    public void Create_WithOldDate_ShouldNotApplyUnspecifiedAgeLimit()
    {
        var oldDate = new DateOnly(1800, 1, 1);

        var result = BirthDate.Create(oldDate, Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(oldDate, result.Value!.Value);
    }

    [Fact]
    public void Create_ShouldUseProvidedReferenceDate()
    {
        var birthDate = new DateOnly(2020, 6, 15);

        var earlierReference = new DateOnly(2020, 6, 14);
        var sameDayReference = new DateOnly(2020, 6, 15);

        var invalidResult = BirthDate.Create(
            birthDate,
            earlierReference);

        var validResult = BirthDate.Create(
            birthDate,
            sameDayReference);

        AssertValidationFailure(
            invalidResult,
            "تاریخ تولد نمی‌تواند در آینده باشد.");

        Assert.True(validResult.IsSuccess);
        Assert.NotNull(validResult.Value);
        Assert.Equal(birthDate, validResult.Value!.Value);
    }

    [Fact]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        var first = CreateValidBirthDate();
        var second = CreateValidBirthDate();

        Assert.NotSame(first, second);

        Assert.True(first.Equals(second));
        Assert.True(second.Equals(first));
        Assert.True(first.Equals((object)second));
    }

    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        var birthDate = CreateValidBirthDate();

        Assert.True(birthDate.Equals(birthDate));
        Assert.True(birthDate.Equals((object)birthDate));
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        var first = CreateValidBirthDate();
        var second = CreateValidBirthDate(ValidDate.AddDays(1));

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
        Assert.False(first.Equals((object)second));
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        var birthDate = CreateValidBirthDate();

        Assert.False(birthDate.Equals((BirthDate?)null));
        Assert.False(birthDate.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        var birthDate = CreateValidBirthDate();

        Assert.False(birthDate.Equals((object)ValidDate));
        Assert.False(birthDate.Equals((object)"1995-05-20"));
        Assert.False(birthDate.Equals(new object()));
    }

    [Fact]
    public void Equals_WithDifferentReferenceDates_ShouldCompareOnlyBirthDate()
    {
        var firstResult = BirthDate.Create(
            ValidDate,
            new DateOnly(2020, 1, 1));

        var secondResult = BirthDate.Create(
            ValidDate,
            Today);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);

        Assert.NotNull(firstResult.Value);
        Assert.NotNull(secondResult.Value);

        Assert.True(firstResult.Value!.Equals(secondResult.Value));

        Assert.Equal(
            firstResult.Value.GetHashCode(),
            secondResult.Value!.GetHashCode());
    }

    [Fact]
    public void EqualityOperators_WithSameValue_ShouldReturnExpectedResults()
    {
        var first = CreateValidBirthDate();
        var second = CreateValidBirthDate();

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void EqualityOperators_WithDifferentValues_ShouldReturnExpectedResults()
    {
        var first = CreateValidBirthDate();
        var second = CreateValidBirthDate(ValidDate.AddDays(1));

        Assert.False(first == second);
        Assert.True(first != second);
    }

    [Fact]
    public void EqualityOperators_WithSameReference_ShouldReturnExpectedResults()
    {
        var first = CreateValidBirthDate();
        var second = first;

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void EqualityOperators_WithBothOperandsNull_ShouldReturnExpectedResults()
    {
        BirthDate? first = null;
        BirthDate? second = null;

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void EqualityOperators_WithOneNullOperand_ShouldReturnExpectedResults()
    {
        var birthDate = CreateValidBirthDate();
        BirthDate? missing = null;

        Assert.False(birthDate == missing);
        Assert.False(missing == birthDate);

        Assert.True(birthDate != missing);
        Assert.True(missing != birthDate);
    }

    [Fact]
    public void GetHashCode_WithEqualValues_ShouldReturnSameHashCode()
    {
        var first = CreateValidBirthDate();
        var second = CreateValidBirthDate();

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithSameInstance_ShouldRemainStable()
    {
        var birthDate = CreateValidBirthDate();

        var firstHash = birthDate.GetHashCode();
        var secondHash = birthDate.GetHashCode();

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void HashSet_WithEqualValues_ShouldKeepOnlyOneItem()
    {
        var dates = new HashSet<BirthDate>
        {
            CreateValidBirthDate(),
            CreateValidBirthDate()
        };

        Assert.Single(dates);
    }

    [Fact]
    public void HashSet_WithDifferentValues_ShouldKeepBothItems()
    {
        var dates = new HashSet<BirthDate>
        {
            CreateValidBirthDate(),
            CreateValidBirthDate(ValidDate.AddDays(1))
        };

        Assert.Equal(2, dates.Count);
    }

    [Fact]
    public void Dictionary_WithEquivalentKey_ShouldFindStoredValue()
    {
        var dates = new Dictionary<BirthDate, string>
        {
            [CreateValidBirthDate()] = "person"
        };

        var found = dates.TryGetValue(
            CreateValidBirthDate(),
            out var value);

        Assert.True(found);
        Assert.Equal("person", value);
    }

    [Fact]
    public void ExplicitDateOnlyConversion_ShouldReturnStoredValue()
    {
        var birthDate = CreateValidBirthDate();

        var result = (DateOnly)birthDate;

        Assert.Equal(ValidDate, result);
    }

    [Fact]
    public void ExplicitDateOnlyConversion_WithNull_ShouldThrowArgumentNullException()
    {
        BirthDate birthDate = null!;

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = (DateOnly)birthDate;
        });
    }

    [Theory]
    [InlineData(1995, 5, 20, "1995-05-20")]
    [InlineData(2000, 2, 29, "2000-02-29")]
    [InlineData(2001, 1, 9, "2001-01-09")]
    public void ToString_ShouldReturnDateInExpectedFormat(
        int year,
        int month,
        int day,
        string expected)
    {
        var birthDate = CreateValidBirthDate(
            new DateOnly(year, month, day));

        var result = birthDate.ToString();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("fa-IR")]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    public void ToString_ShouldNotDependOnCurrentCulture(
        string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo(cultureName);

            var birthDate = CreateValidBirthDate();

            Assert.Equal("1995-05-20", birthDate.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static BirthDate CreateValidBirthDate(
        DateOnly? value = null)
    {
        var result = BirthDate.Create(
            value ?? ValidDate,
            Today);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        return result.Value!;
    }

    private static void AssertValidationFailure(
        Result<BirthDate> result,
        string expectedMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Value);
    }
}
