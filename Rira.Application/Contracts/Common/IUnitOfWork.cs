using Rira.Domain.Common;

namespace Rira.Application.Contracts.Common;

public interface IUnitOfWork
{
    /// <summary>
    /// Saves pending changes atomically. On failure or cancellation, discards pending
    /// changes before returning or throwing. Subsequent operations must reload entities.
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards pending additions, updates and deletions after an unsuccessful operation.
    /// Restores persisted scalar values of tracked entities and ends the tracking session.
    /// Does not undo domain events or database commits. Safe to call repeatedly.
    /// Use for failures before SaveChangesAsync; saving handles its own cleanup.
    /// </summary>
    void DiscardChanges();
}
