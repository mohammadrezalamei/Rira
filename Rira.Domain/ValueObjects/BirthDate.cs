using Rira.Domain.Common;

namespace Rira.Domain.ValueObjects;

public sealed class BirthDate : ValueObject<BirthDate>
{
    public DateOnly Value { get; }

    private BirthDate(DateOnly value)
    {
        Value = value;
    }

    public static Result<BirthDate> Create(
        DateOnly value,
        DateOnly today)
    {
        if (today == default)
            return Result<BirthDate>.Failure(
                "تاریخ مرجع اعتبارسنجی مشخص نشده است.",
                ErrorType.Validation);

        if (value == default)
            return Result<BirthDate>.Failure(
                "تاریخ تولد باید مشخص شود.",
                ErrorType.Validation);

        if (value > today)
            return Result<BirthDate>.Failure(
                "تاریخ تولد نمی‌تواند در آینده باشد.",
                ErrorType.Validation);

        return Result<BirthDate>.Success(new BirthDate(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override bool Equals(object? obj) => base.Equals(obj);

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(BirthDate? left, BirthDate? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(BirthDate? left, BirthDate? right)
        => !(left == right);

    public static explicit operator DateOnly(BirthDate birthDate)
    {
        ArgumentNullException.ThrowIfNull(birthDate);

        return birthDate.Value;
    }

    public override string ToString()
    {
        return Value.ToString(
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
