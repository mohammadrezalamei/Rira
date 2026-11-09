using Rira.Application.Contracts.Services;
using Rira.Application.Services.Commands;
using Rira.Application.Services.Queries;
using Rira.Endpoint.Interceptors;
using Rira.Endpoint.Services;
using Rira.Infrastructure;

namespace Rira.Endpoint;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString =
            builder.Configuration.GetConnectionString("Rira");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Rira' is not configured.");
        }

        builder.Services.AddInfrastructure(connectionString);

        builder.Services.AddScoped<
            IPersonCommandService,
            PersonCommandService>();

        builder.Services.AddScoped<
            IPersonQueryService,
            PersonQueryService>();

        builder.Services.AddTransient<GrpcExceptionInterceptor>();

        builder.Services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = false;
            options.Interceptors.Add<GrpcExceptionInterceptor>();
        });

        var app = builder.Build();

        app.MapGrpcService<PersonGrpcService>();

        app.MapGet("/", () =>
            "Rira gRPC host is running. Use a gRPC client to call the services.");

        app.Run();
    }
}
