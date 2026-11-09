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
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            DiscardChanges();
            return Result.Failure("اطلاعات توسط درخواست دیگری تغییر کرده است؛ دوباره دریافت کنید.", ErrorType.Conflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            DiscardChanges();
            return Result.Failure("شخصی با این کد ملی قبلاً ثبت شده است.", ErrorType.Conflict);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to save person changes.");
            DiscardChanges();
            return Result.Failure("ذخیره اطلاعات با خطا مواجه شد.", ErrorType.ServerError);
        }
        catch
        {
            DiscardChanges();
            throw;
        }
    }

    public void DiscardChanges()
    {
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                entry.CurrentValues.SetValues(entry.OriginalValues);
                entry.State = EntityState.Unchanged;
            }
        }
        // A failed operation ends this tracking session. Subsequent reads reload current data.
        context.ChangeTracker.Clear();
    }
}
