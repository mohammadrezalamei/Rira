using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rira.Endpoint.Protos;
using Rira.Infrastructure.Persistence;
using Xunit;
using ProtoDate = Rira.Endpoint.Protos.Date;

namespace Rira.Test.Endpoint;

public sealed class PersonGrpcIntegrationTests : IAsyncLifetime
{
    private const string FirstCode = "0012345679";
    private const string SecondCode = "1234567891";

    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");

    // Use the actual entry point from Rira.Endpoint.
    private readonly WebApplicationFactory<global::Rira.Endpoint.Program>
        _rootFactory = new();

    private WebApplicationFactory<global::Rira.Endpoint.Program>
        _factory = null!;

    private GrpcChannel _channel = null!;
    private PersonService.PersonServiceClient _client = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _factory = _rootFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.UseSetting(
                "ConnectionStrings:Rira",
                "Data Source=:memory:");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<RiraDbContext>();

                services.RemoveAll<
                    DbContextOptions<RiraDbContext>>();

                services.RemoveAll<
                    IDbContextOptionsConfiguration<RiraDbContext>>();

                services.AddDbContext<RiraDbContext>(options =>
                    options.UseSqlite(_connection));
            });
        });

        // Accessing Services starts the test host.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RiraDbContext>();

            await db.Database.MigrateAsync();
        }

        _channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpHandler = _factory.Server.CreateHandler(),
                DisposeHttpClient = true
            });

        _client = new PersonService.PersonServiceClient(_channel);
    }

    public async Task DisposeAsync()
    {
        try
        {
            _channel?.Dispose();

            // Also disposes the child factory made by WithWebHostBuilder.
            await _rootFactory.DisposeAsync();
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // Create
    // ------------------------------------------------------------

    [Fact]
    public async Task CreatePerson_ShouldReturnAndPersistPerson()
    {
        var request = ValidRequest();

        var response = await _client.CreatePersonAsync(request);

        Assert.True(Guid.TryParse(response.Id, out var id));
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(request.FirstName, response.FirstName);
        Assert.Equal(request.LastName, response.LastName);
        Assert.Equal(request.NationalCode, response.NationalCode);
        Assert.Equal(request.BirthDate, response.BirthDate);

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RiraDbContext>();

        var stored = await db.Persons
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(id, stored.Id);
        Assert.Equal(request.FirstName, stored.FirstName);
        Assert.Equal(request.LastName, stored.LastName);
        Assert.Equal(FirstCode, stored.NationalCode.Value);
        Assert.Equal(
            new DateOnly(1995, 5, 21),
            stored.BirthDate.Value);
    }

    [Theory]
    [InlineData("0012345679")]
    [InlineData(" 0012345679 ")]
    [InlineData("۰۰۱۲۳۴۵۶۷۹")]
    [InlineData("٠٠١٢٣٤٥٦٧٩")]
    public async Task CreatePerson_ShouldNormalizeInput(string code)
    {
        var request = ValidRequest(code);
        request.FirstName = " علی ";
        request.LastName = " احمدی ";

        var response = await _client.CreatePersonAsync(request);

        Assert.Equal("علی", response.FirstName);
        Assert.Equal("احمدی", response.LastName);
        Assert.Equal(FirstCode, response.NationalCode);

        var fetched = await GetByCodeAsync(code);

        Assert.Equal(response, fetched);
    }

    [Fact]
    public async Task CreatePerson_ShouldAcceptNamesWith100Characters()
    {
        var request = ValidRequest();
        request.FirstName = new string('a', 100);
        request.LastName = new string('b', 100);

        var created = await _client.CreatePersonAsync(request);
        var fetched = await GetByIdAsync(created.Id);

        Assert.Equal(request.FirstName, fetched.FirstName);
        Assert.Equal(request.LastName, fetched.LastName);
        Assert.Equal(1, await CountAsync());
    }

    [Theory]
    [InlineData(FirstCode)]
    [InlineData("۰۰۱۲۳۴۵۶۷۹")]
    public async Task CreatePerson_WhenCodeExists_ShouldReturnAlreadyExists(
        string code)
    {
        var original = await CreateAsync();

        await AssertStatusAsync(
            StatusCode.AlreadyExists,
            () => _client.CreatePersonAsync(
                ValidRequest(code)).ResponseAsync);

        Assert.Equal(1, await CountAsync());
        Assert.Equal(original, await GetByIdAsync(original.Id));
    }

    // ------------------------------------------------------------
    // Read
    // ------------------------------------------------------------

    [Fact]
    public async Task GetPersonById_ShouldReturnMatchingPerson()
    {
        var first = await CreateAsync(FirstCode);
        await CreateAsync(SecondCode);

        var fetched = await GetByIdAsync(first.Id);

        Assert.Equal(first, fetched);
    }

    [Fact]
    public async Task GetPersonByNationalCode_ShouldReturnMatchingPerson()
    {
        var first = await CreateAsync(FirstCode);
        await CreateAsync(SecondCode);

        var fetched = await GetByCodeAsync(FirstCode);

        Assert.Equal(first, fetched);
    }

    [Fact]
    public async Task GetAllPersons_WhenEmpty_ShouldReturnEmptyList()
    {
        var response = await _client.GetAllPersonsAsync(new Empty());

        Assert.Empty(response.Persons);
    }

    [Fact]
    public async Task GetAllPersons_ShouldReturnAllPersons()
    {
        var first = await CreateAsync(FirstCode);
        var second = await CreateAsync(SecondCode);

        var response = await _client.GetAllPersonsAsync(new Empty());

        Assert.Equal(2, response.Persons.Count);

        Assert.Equal(
            first,
            Assert.Single(response.Persons, x => x.Id == first.Id));

        Assert.Equal(
            second,
            Assert.Single(response.Persons, x => x.Id == second.Id));
    }

    // ------------------------------------------------------------
    // Update
    // ------------------------------------------------------------

    [Theory]
    [InlineData(FirstCode)]
    [InlineData(SecondCode)]
    public async Task UpdatePerson_ShouldPersistChanges(string newCode)
    {
        var original = await CreateAsync();

        var payload = ValidRequest(newCode);
        payload.FirstName = " مریم ";
        payload.LastName = " رضایی ";
        payload.BirthDate = new ProtoDate
        {
            Year = 2000,
            Month = 2,
            Day = 29
        };

        var response = await _client.UpdatePersonAsync(
            ToUpdate(original.Id, payload));

        Assert.Equal(original.Id, response.Id);
        Assert.Equal("مریم", response.FirstName);
        Assert.Equal("رضایی", response.LastName);
        Assert.Equal(newCode, response.NationalCode);
        Assert.Equal(payload.BirthDate, response.BirthDate);

        Assert.Equal(response, await GetByIdAsync(original.Id));
        Assert.Equal(response, await GetByCodeAsync(newCode));
        Assert.Equal(1, await CountAsync());

        if (newCode != FirstCode)
        {
            await AssertStatusAsync(
                StatusCode.NotFound,
                () => GetByCodeAsync(FirstCode));
        }
    }

    [Fact]
    public async Task UpdatePerson_WhenCodeBelongsToAnotherPerson_ShouldNotChangeData()
    {
        var first = await CreateAsync(FirstCode);
        var second = await CreateAsync(SecondCode);

        var payload = ValidRequest(SecondCode);
        payload.FirstName = "نام جدید";

        await AssertStatusAsync(
            StatusCode.AlreadyExists,
            () => _client.UpdatePersonAsync(
                ToUpdate(first.Id, payload)).ResponseAsync);

        Assert.Equal(first, await GetByIdAsync(first.Id));
        Assert.Equal(second, await GetByIdAsync(second.Id));
        Assert.Equal(2, await CountAsync());
    }

    // ------------------------------------------------------------
    // Delete
    // ------------------------------------------------------------

    [Fact]
    public async Task DeletePerson_ShouldRemoveOnlyTargetPerson()
    {
        var target = await CreateAsync(FirstCode);
        var other = await CreateAsync(SecondCode);

        await DeleteAsync(target.Id);

        Assert.Equal(1, await CountAsync());
        Assert.Equal(other, await GetByIdAsync(other.Id));

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => GetByIdAsync(target.Id));

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => GetByCodeAsync(FirstCode));

        var response = await _client.GetAllPersonsAsync(new Empty());

        Assert.Equal(other, Assert.Single(response.Persons));
    }

    [Fact]
    public async Task DeletePerson_WhenAlreadyDeleted_ShouldReturnNotFound()
    {
        var created = await CreateAsync();

        await DeleteAsync(created.Id);

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => DeleteAsync(created.Id));

        Assert.Equal(0, await CountAsync());
    }

    // ------------------------------------------------------------
    // Missing records
    // ------------------------------------------------------------

    [Fact]
    public async Task MissingPerson_ShouldReturnNotFound()
    {
        var id = Guid.NewGuid().ToString();

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => GetByIdAsync(id));

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => GetByCodeAsync(FirstCode));

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => _client.UpdatePersonAsync(
                ToUpdate(id, ValidRequest())).ResponseAsync);

        await AssertStatusAsync(
            StatusCode.NotFound,
            () => DeleteAsync(id));

        Assert.Equal(0, await CountAsync());
    }

    // ------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidId_ShouldReturnInvalidArgument(string id)
    {
        await AssertStatusAsync(
            StatusCode.InvalidArgument,
            () => GetByIdAsync(id));

        await AssertStatusAsync(
            StatusCode.InvalidArgument,
            () => _client.UpdatePersonAsync(
                ToUpdate(id, ValidRequest())).ResponseAsync);

        await AssertStatusAsync(
            StatusCode.InvalidArgument,
            () => DeleteAsync(id));

        Assert.Equal(0, await CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("12345678912")]
    [InlineData("abcdefghij")]
    [InlineData("1111111111")]
    [InlineData("1234567890")]
    public async Task GetByInvalidCode_ShouldReturnInvalidArgument(
        string code)
    {
        await AssertStatusAsync(
            StatusCode.InvalidArgument,
            () => GetByCodeAsync(code));
    }

    [Theory]
    [InlineData("first-empty")]
    [InlineData("first-whitespace")]
    [InlineData("first-too-long")]
    [InlineData("last-empty")]
    [InlineData("last-whitespace")]
    [InlineData("last-too-long")]
    [InlineData("code-empty")]
    [InlineData("code-whitespace")]
    [InlineData("code-short")]
    [InlineData("code-long")]
    [InlineData("code-letters")]
    [InlineData("code-repeated")]
    [InlineData("code-checksum")]
    [InlineData("date-missing")]
    [InlineData("date-zero")]
    [InlineData("date-default")]
    [InlineData("date-month")]
    [InlineData("date-day")]
    [InlineData("date-leap")]
    [InlineData("date-year")]
    [InlineData("date-future")]
    public async Task InvalidPayload_ShouldRejectCreateAndUpdateWithoutChangingData(
        string scenario)
    {
        var invalid = InvalidRequest(scenario);

        await AssertStatusAsync(
            StatusCode.InvalidArgument,
            () => _client.CreatePersonAsync(invalid).ResponseAsync);

        Assert.Equal(0, await CountAsync());

        var original = await CreateAsync();

        await AssertStatusAsync(
            StatusCode.InvalidArgument,
            () => _client.UpdatePersonAsync(
                ToUpdate(original.Id, invalid)).ResponseAsync);

        Assert.Equal(original, await GetByIdAsync(original.Id));
        Assert.Equal(1, await CountAsync());
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private async Task<PersonResponse> CreateAsync(
        string code = FirstCode)
    {
        return await _client.CreatePersonAsync(ValidRequest(code));
    }

    private async Task<PersonResponse> GetByIdAsync(string id)
    {
        return await _client.GetPersonByIdAsync(
            new GetPersonByIdRequest
            {
                Id = id
            });
    }

    private async Task<PersonResponse> GetByCodeAsync(string code)
    {
        return await _client.GetPersonByNationalCodeAsync(
            new GetPersonByNationalCodeRequest
            {
                NationalCode = code
            });
    }

    private async Task DeleteAsync(string id)
    {
        await _client.DeletePersonAsync(
            new DeletePersonRequest
            {
                Id = id
            });
    }

    private async Task<int> CountAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RiraDbContext>();

        return await db.Persons.CountAsync();
    }

    private static async Task AssertStatusAsync(
        StatusCode expected,
        Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<RpcException>(action);

        Assert.Equal(expected, exception.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(exception.Status.Detail));
    }

    private static CreatePersonRequest ValidRequest(
        string code = FirstCode)
    {
        return new CreatePersonRequest
        {
            FirstName = "علی",
            LastName = "احمدی",
            NationalCode = code,
            BirthDate = new ProtoDate
            {
                Year = 1995,
                Month = 5,
                Day = 21
            }
        };
    }

    private static UpdatePersonRequest ToUpdate(
        string id,
        CreatePersonRequest source)
    {
        var request = new UpdatePersonRequest
        {
            Id = id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            NationalCode = source.NationalCode
        };

        if (source.BirthDate is not null)
        {
            request.BirthDate = source.BirthDate.Clone();
        }

        return request;
    }

    private static CreatePersonRequest InvalidRequest(string scenario)
    {
        var request = ValidRequest();

        switch (scenario)
        {
            case "first-empty":
                request.FirstName = "";
                break;

            case "first-whitespace":
                request.FirstName = "   ";
                break;

            case "first-too-long":
                request.FirstName = new string('a', 101);
                break;

            case "last-empty":
                request.LastName = "";
                break;

            case "last-whitespace":
                request.LastName = "   ";
                break;

            case "last-too-long":
                request.LastName = new string('a', 101);
                break;

            case "code-empty":
                request.NationalCode = "";
                break;

            case "code-whitespace":
                request.NationalCode = "   ";
                break;

            case "code-short":
                request.NationalCode = "123";
                break;

            case "code-long":
                request.NationalCode = "12345678912";
                break;

            case "code-letters":
                request.NationalCode = "abcdefghij";
                break;

            case "code-repeated":
                request.NationalCode = "1111111111";
                break;

            case "code-checksum":
                request.NationalCode = "1234567890";
                break;

            case "date-missing":
                request.BirthDate = null;
                break;

            case "date-zero":
                request.BirthDate = new ProtoDate();
                break;

            case "date-default":
                request.BirthDate = new ProtoDate
                {
                    Year = 1,
                    Month = 1,
                    Day = 1
                };
                break;

            case "date-month":
                request.BirthDate.Month = 13;
                break;

            case "date-day":
                request.BirthDate.Month = 4;
                request.BirthDate.Day = 31;
                break;

            case "date-leap":
                request.BirthDate = new ProtoDate
                {
                    Year = 2023,
                    Month = 2,
                    Day = 29
                };
                break;

            case "date-year":
                request.BirthDate.Year = 10000;
                break;

            case "date-future":
                {
                    var future = DateOnly.FromDateTime(
                        DateTime.UtcNow.AddYears(1));

                    request.BirthDate = new ProtoDate
                    {
                        Year = future.Year,
                        Month = future.Month,
                        Day = future.Day
                    };

                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unknown validation scenario.");
        }

        return request;
    }
}
