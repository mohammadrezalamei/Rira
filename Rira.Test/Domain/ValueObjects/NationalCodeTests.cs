using System;
using System.Collections.Generic;
using Rira.Domain.Common;
using Rira.Domain.ValueObjects;
using Xunit;

namespace Rira.Tests.Domain.ValueObjects;

public class NationalCodeTests
{
    private const string ValidCode = "0123456789";
    private const string AnotherValidCode = "1234567891";

    [Fact]
    public void Create_WithValidCode_ShouldReturnSuccess()
    {
        var result = NationalCode.Create(ValidCode);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(ValidCode, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("     ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("\u00A0")]
    public void Create_WithNullOrWhitespace_ShouldReturnValidationFailure(
        string? input)
    {
        var result = NationalCode.Create(input);

        AssertValidationFailure(
            result,
            "کد ملی نمی‌تواند خالی باشد.");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("12345678")]
    [InlineData("012345678")]
    [InlineData("00123456789")]
    [InlineData("01234567890123456789")]
    [InlineData("۰۱۲۳۴۵۶۷۸")]
    public void Create_WithInvalidLength_ShouldReturnValidationFailure(
        string input)
    {
        var result = NationalCode.Create(input);

        AssertValidationFailure(
            result,
            "کد ملی باید دقیقاً ۱۰ رقم باشد.");
    }

    [Fact]
    public void Create_WithShortCode_ShouldNotAddLeadingZeros()
    {
        // نسخه ده‌رقمی این ورودی یعنی 0123456789 معتبر است.
        var result = NationalCode.Create("123456789");

        AssertValidationFailure(
            result,
            "کد ملی باید دقیقاً ۱۰ رقم باشد.");
    }

    [Theory]
    [InlineData("A123456789")]
    [InlineData("012345678A")]
    [InlineData("01234 6789")]
    [InlineData("01234-6789")]
    [InlineData("01234.6789")]
    [InlineData("+123456789")]
    [InlineData("-123456789")]
    [InlineData("01234\t6789")]
    [InlineData("01234\n6789")]
    [InlineData("01234\06789")]
    [InlineData("01234\u200C6789")]
    [InlineData("01234\u200B6789")]
    [InlineData("０１２３４５６７８９")]
    public void Create_WithNonDigitCharacters_ShouldReturnValidationFailure(
        string input)
    {
        var result = NationalCode.Create(input);

        AssertValidationFailure(
            result,
            "کد ملی فقط می‌تواند شامل رقم باشد.");
    }

    [Theory]
    [InlineData("0000000000")]
    [InlineData("1111111111")]
    [InlineData("2222222222")]
    [InlineData("3333333333")]
    [InlineData("4444444444")]
    [InlineData("5555555555")]
    [InlineData("6666666666")]
    [InlineData("7777777777")]
    [InlineData("8888888888")]
    [InlineData("9999999999")]
    [InlineData("۰۰۰۰۰۰۰۰۰۰")]
    [InlineData("١١١١١١١١١١")]
    [InlineData("0۰٠0۰٠0۰٠0")]
    public void Create_WithRepeatedDigits_ShouldReturnValidationFailure(
        string input)
    {
        var result = NationalCode.Create(input);

        AssertValidationFailure(
            result,
            "کد ملی نمی‌تواند شامل ارقام کاملاً یکسان باشد.");
    }

    [Theory]
    [InlineData("0123456789")]
    [InlineData("۰۱۲۳۴۵۶۷۸۹")]
    [InlineData("٠١٢٣٤٥٦٧٨٩")]
    [InlineData("۰1٢۳4٥۶7٨۹")]
    public void Create_WithSupportedDigitFormats_ShouldNormalizeToEnglish(
        string input)
    {
        var result = NationalCode.Create(input);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ValidCode, result.Value.Value);
    }

    [Theory]
    [InlineData(" 0123456789")]
    [InlineData("0123456789 ")]
    [InlineData("  0123456789  ")]
    [InlineData("\t0123456789\r\n")]
    [InlineData("\u00A00123456789\u00A0")]
    [InlineData("  ۰۱۲۳۴۵۶۷۸۹  ")]
    public void Create_WithSurroundingWhitespace_ShouldTrimInput(
        string input)
    {
        var result = NationalCode.Create(input);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ValidCode, result.Value.Value);
    }

    [Fact]
    public void Create_WithLeadingZero_ShouldPreserveIt()
    {
        var result = NationalCode.Create(ValidCode);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(10, result.Value.Value.Length);
        Assert.StartsWith("0", result.Value.Value);
        Assert.Equal(ValidCode, result.Value.Value);
    }

    [Theory]
    // باقی‌مانده مجموع وزنی: صفر تا ده
    [InlineData("0000000140")] // 11 % 11 = 0
    [InlineData("0000000061")] // 12 % 11 = 1
    [InlineData("0000000019")] //  2 % 11 = 2
    [InlineData("0000000078")] // 14 % 11 = 3
    [InlineData("0000000027")] //  4 % 11 = 4
    [InlineData("0000000086")] // 16 % 11 = 5
    [InlineData("0000000035")] //  6 % 11 = 6
    [InlineData("0000000094")] // 18 % 11 = 7
    [InlineData("0000000043")] //  8 % 11 = 8
    [InlineData("0000000302")] //  9 % 11 = 9
    [InlineData("0000000051")] // 10 % 11 = 10
    [InlineData("0123456789")]
    [InlineData("1234567891")]
    public void Create_WithValidCheckDigit_ShouldSucceed(
        string input)
    {
        var result = NationalCode.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.NotNull(result.Value);
        Assert.Equal(input, result.Value.Value);
    }

    [Theory]
    [InlineData("0000000141")] // باقی‌مانده صفر، رقم کنترل نادرست
    [InlineData("0000000060")] // باقی‌مانده یک، رقم کنترل نادرست
    [InlineData("0000000012")] // باقی‌مانده دو، رقم کنترل نادرست
    [InlineData("0123456780")]
    [InlineData("0123456781")]
    [InlineData("0123456782")]
    [InlineData("0123456783")]
    [InlineData("0123456784")]
    [InlineData("0123456785")]
    [InlineData("0123456786")]
    [InlineData("0123456787")]
    [InlineData("0123456788")]
    [InlineData("۰۱۲۳۴۵۶۷۸۰")]
    [InlineData("٠١٢٣٤٥٦٧٨٠")]
    public void Create_WithInvalidCheckDigit_ShouldReturnValidationFailure(
        string input)
    {
        var result = NationalCode.Create(input);

        AssertValidationFailure(
            result,
            "کد ملی واردشده معتبر نیست.");
    }

    [Theory]
    [InlineData("1123456789")]
    [InlineData("0223456789")]
    [InlineData("0133456789")]
    [InlineData("0124456789")]
    [InlineData("0123556789")]
    [InlineData("0123466789")]
    [InlineData("0123457789")]
    [InlineData("0123456889")]
    [InlineData("0123456799")]
    public void Create_WhenOneBodyDigitChanges_ShouldRejectInvalidChecksum(
        string input)
    {
        var result = NationalCode.Create(input);

        AssertValidationFailure(
            result,
            "کد ملی واردشده معتبر نیست.");
    }

    [Fact]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        var first = CreateValidCode();
        var second = CreateValidCode();

        Assert.NotSame(first, second);
        Assert.True(first.Equals(second));
        Assert.True(second.Equals(first));
        Assert.True(first.Equals((object)second));
    }

    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        var nationalCode = CreateValidCode();

        Assert.True(nationalCode.Equals(nationalCode));
        Assert.True(nationalCode.Equals((object)nationalCode));
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        var first = CreateValidCode();
        var second = CreateValidCode(AnotherValidCode);

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
        Assert.False(first.Equals((object)second));
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        var nationalCode = CreateValidCode();

        Assert.False(nationalCode.Equals((NationalCode?)null));
        Assert.False(nationalCode.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        var nationalCode = CreateValidCode();

        Assert.False(nationalCode.Equals((object)ValidCode));
        Assert.False(nationalCode.Equals(new object()));
    }

    [Theory]
    [InlineData("۰۱۲۳۴۵۶۷۸۹")]
    [InlineData("٠١٢٣٤٥٦٧٨٩")]
    [InlineData(" 0123456789 ")]
    public void Equals_WithEquivalentNormalizedValues_ShouldReturnTrue(
        string input)
    {
        var first = CreateValidCode();
        var second = CreateValidCode(input);

        Assert.True(first.Equals(second));
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void EqualityOperators_WithSameValue_ShouldReturnExpectedResults()
    {
        var first = CreateValidCode();
        var second = CreateValidCode();

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void EqualityOperators_WithDifferentValues_ShouldReturnExpectedResults()
    {
        var first = CreateValidCode();
        var second = CreateValidCode(AnotherValidCode);

        Assert.False(first == second);
        Assert.True(first != second);
    }

    [Fact]
    public void EqualityOperators_WithSameReference_ShouldReturnExpectedResults()
    {
        var first = CreateValidCode();
        var second = first;

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void EqualityOperators_WithBothOperandsNull_ShouldReturnExpectedResults()
    {
        NationalCode? first = null;
        NationalCode? second = null;

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void EqualityOperators_WithOneNullOperand_ShouldReturnExpectedResults()
    {
        var nationalCode = CreateValidCode();
        NationalCode? missing = null;

        Assert.False(nationalCode == missing);
        Assert.False(missing == nationalCode);

        Assert.True(nationalCode != missing);
        Assert.True(missing != nationalCode);
    }

    [Fact]
    public void GetHashCode_WithEqualValues_ShouldReturnSameHashCode()
    {
        var first = CreateValidCode();
        var second = CreateValidCode();

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithSameInstance_ShouldRemainStable()
    {
        var nationalCode = CreateValidCode();

        var firstHash = nationalCode.GetHashCode();
        var secondHash = nationalCode.GetHashCode();

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void HashSet_WithEquivalentValues_ShouldKeepOnlyOneItem()
    {
        var values = new HashSet<NationalCode>
        {
            CreateValidCode(),
            CreateValidCode("۰۱۲۳۴۵۶۷۸۹"),
            CreateValidCode(" 0123456789 ")
        };

        Assert.Single(values);
    }

    [Fact]
    public void HashSet_WithDifferentValues_ShouldKeepBothItems()
    {
        var values = new HashSet<NationalCode>
        {
            CreateValidCode(),
            CreateValidCode(AnotherValidCode)
        };

        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void Dictionary_WithEquivalentKey_ShouldFindStoredValue()
    {
        var values = new Dictionary<NationalCode, string>
        {
            [CreateValidCode()] = "person"
        };

        var found = values.TryGetValue(
            CreateValidCode("۰۱۲۳۴۵۶۷۸۹"),
            out var value);

        Assert.True(found);
        Assert.Equal("person", value);
    }

    [Fact]
    public void ExplicitStringConversion_ShouldReturnNormalizedValue()
    {
        var nationalCode = CreateValidCode("۰۱۲۳۴۵۶۷۸۹");

        var result = (string)nationalCode;

        Assert.Equal(ValidCode, result);
    }

    [Fact]
    public void ExplicitStringConversion_WithNull_ShouldThrowArgumentNullException()
    {
        NationalCode nationalCode = null!;

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = (string)nationalCode;
        });
    }

    [Fact]
    public void ToString_ShouldReturnNormalizedValue()
    {
        var nationalCode = CreateValidCode(" ۰۱۲۳۴۵۶۷۸۹ ");

        var result = nationalCode.ToString();

        Assert.Equal(ValidCode, result);
    }

    private static NationalCode CreateValidCode(
        string input = ValidCode)
    {
        var result = NationalCode.Create(input);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        return result.Value!;
    }

    private static void AssertValidationFailure(
        Result<NationalCode> result,
        string expectedMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Value);
    }
}
