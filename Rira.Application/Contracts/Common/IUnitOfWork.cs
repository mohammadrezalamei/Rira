using Rira.Domain.Common;

namespace Rira.Application.Contracts.Common;

public interface IUnitOfWork
{
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards pending additions, updates and deletions after an unsuccessful operation.
    /// Implementations must restore tracked state and must not commit partial changes.
    /// </summary>
    void DiscardChanges();
}
