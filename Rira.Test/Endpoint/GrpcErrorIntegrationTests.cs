using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Rira.Application.Contracts.Services;
using Rira.Application.DTOs.Persons;
using Rira.Domain.Common;
using Rira.Endpoint.Protos;

namespace Rira.Tests.Endpoint;

public class GrpcErrorIntegrationTests
{
    [Theory]
    [InlineData("duplicate", StatusCode.AlreadyExists)]
    [InlineData("concurrency", StatusCode.Aborted)]
    [InlineData("server-error", StatusCode.Internal)]
    [InlineData("exception", StatusCode.Internal)]
    public async Task Failure_ReachesClientWithExpectedStatusAndSafeDetails(
        string scenario, StatusCode expectedStatus)
    {
        const string details = "Database connection and SQL details";
        var service = new Mock<IPersonQueryService>(MockBehavior.Strict);
        var id = Guid.NewGuid();
        var setup = service.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()));
        if (scenario == "exception")
            setup.ThrowsAsync(new InvalidOperationException(details));
        else
        {
            var type = scenario switch
            {
                "duplicate" => ErrorType.Conflict,
                "concurrency" => ErrorType.ConcurrencyConflict,
                _ => ErrorType.ServerError
            };
            setup.ReturnsAsync(Result<PersonResponseDto>.Failure(details, type));
        }

        await using var root = new WebApplicationFactory<global::Rira.Endpoint.Program>();
        await using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPersonQueryService>();
                services.AddScoped<IPersonQueryService>(_ => service.Object);
            }));
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
            DisposeHttpClient = true
        });
        var client = new PersonService.PersonServiceClient(channel);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            client.GetPersonByIdAsync(new GetPersonByIdRequest { Id = id.ToString() }).ResponseAsync);

        Assert.Equal(expectedStatus, exception.StatusCode);
        if (expectedStatus == StatusCode.Internal)
        {
            Assert.False(string.IsNullOrWhiteSpace(exception.Status.Detail));
            Assert.DoesNotContain(details, exception.Status.Detail);
            Assert.DoesNotContain(nameof(InvalidOperationException), exception.Status.Detail);
        }
        else
            Assert.Equal(details, exception.Status.Detail);
        service.Verify(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
