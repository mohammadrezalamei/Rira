using Rira.Domain.Common;

namespace Rira.Domain.ValueObjects;

public sealed class NationalCode : ValueObject<NationalCode>
{
    public const int RequiredLength = 10;

    public string Value { get; }

    private NationalCode(string value)
    {
        Value = value;
    }

    public static Result<NationalCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<NationalCode>.Failure(
                "کد ملی نمی‌تواند خالی باشد.",
                ErrorType.Validation);

        var normalizedValue = NormalizeDigits(value.Trim());

        if (normalizedValue.Length != RequiredLength)
            return Result<NationalCode>.Failure(
                "کد ملی باید دقیقاً ۱۰ رقم باشد.",
                ErrorType.Validation);

        if (normalizedValue.Any(character => character is < '0' or > '9'))
            return Result<NationalCode>.Failure(
                "کد ملی فقط می‌تواند شامل رقم باشد.",
                ErrorType.Validation);

        if (normalizedValue.All(character => character == normalizedValue[0]))
            return Result<NationalCode>.Failure(
                "کد ملی نمی‌تواند شامل ارقام کاملاً یکسان باشد.",
                ErrorType.Validation);

        if (!HasValidCheckDigit(normalizedValue))
            return Result<NationalCode>.Failure(
                "کد ملی واردشده معتبر نیست.",
                ErrorType.Validation);

        return Result<NationalCode>.Success(
            new NationalCode(normalizedValue));
    }

    private static string NormalizeDigits(string value)
    {
        var characters = value.ToCharArray();

        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = characters[index] switch
            {
                >= '۰' and <= '۹' =>
                    (char)('0' + characters[index] - '۰'),

                >= '٠' and <= '٩' =>
                    (char)('0' + characters[index] - '٠'),

                _ => characters[index]
            };
        }

        return new string(characters);
    }

    private static bool HasValidCheckDigit(string value)
    {
        var sum = 0;

        for (var index = 0; index < RequiredLength - 1; index++)
        {
            var digit = value[index] - '0';
            sum += digit * (RequiredLength - index);
        }

        var remainder = sum % 11;
        var expectedCheckDigit = remainder < 2
            ? remainder
            : 11 - remainder;

        var actualCheckDigit = value[^1] - '0';

        return actualCheckDigit == expectedCheckDigit;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override bool Equals(object? obj) => base.Equals(obj);

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(
        NationalCode? left,
        NationalCode? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(
        NationalCode? left,
        NationalCode? right) => !(left == right);

    public static explicit operator string(NationalCode nationalCode)
    {
        ArgumentNullException.ThrowIfNull(nationalCode);

        return nationalCode.Value;
    }

    public override string ToString() => Value;
}
