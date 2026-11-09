using Rira.Domain.Common;

namespace Rira.Application.Contracts.Common;

public interface IUnitOfWork
{
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default);
}
