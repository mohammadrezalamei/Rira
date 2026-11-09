namespace Rira.Domain.Common;

public abstract class ValueObject<T> : IEquatable<T> where T : ValueObject<T>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        return obj is T other && Equals(other);
    }

    public bool Equals(T? other)
    {
        if (other is null || GetType() != other.GetType())
            return false;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) => unchecked(current * 23 + (obj?.GetHashCode() ?? 0)));
    }

    public static bool operator ==(ValueObject<T>? left, ValueObject<T>? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(ValueObject<T>? left, ValueObject<T>? right) => !(left == right);
}
