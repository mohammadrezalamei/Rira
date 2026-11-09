using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rira.Application.Contracts.Common;
using Rira.Application.Contracts.Persistence;
using Rira.Domain.Repositories;
using Rira.Infrastructure.Persistence;
using Rira.Infrastructure.Repositories.Commands;
using Rira.Infrastructure.Repositories.Queries;

namespace Rira.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddLogging();
        services.AddDbContext<RiraDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IPersonReadRepository, PersonReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
