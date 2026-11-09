namespace Rira.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
