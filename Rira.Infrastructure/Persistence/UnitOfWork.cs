using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rira.Application.Contracts.Common;
using Rira.Domain.Common;

namespace Rira.Infrastructure.Persistence;

public sealed class UnitOfWork(RiraDbContext context, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var committed = false;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            committed = true;
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("اطلاعات توسط درخواست دیگری تغییر کرده است؛ دوباره دریافت کنید.", ErrorType.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            return Result.Failure("اطلاعات تکراری است؛ مقدار فیلدهای یکتا را بررسی کنید.", ErrorType.Conflict);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to save person changes.");
            return Result.Failure("ذخیره اطلاعات با خطا مواجه شد.", ErrorType.ServerError);
        }
        finally
        {
            if (!committed)
                DiscardChanges();
        }
    }

    public void DiscardChanges()
    {
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                entry.CurrentValues.SetValues(entry.OriginalValues);
        }
        // A failed operation ends this tracking session. Subsequent reads reload current data.
        context.ChangeTracker.Clear();
    }
}
