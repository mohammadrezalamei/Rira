using Moq;
using Rira.Application.Contracts.Persistence;
using Rira.Application.DTOs.Persons;
using Rira.Application.Services.Queries;
using Rira.Domain.Common;
using Rira.Domain.ValueObjects;
using Xunit;

namespace Rira.Tests.Application.Queries;

public class PersonQueryServiceTests
{
    private const string ValidNationalCode = "0123456789";

    private readonly Mock<IPersonReadRepository> _repository;
    private readonly PersonQueryService _service;

    public PersonQueryServiceTests()
    {
        _repository = new Mock<IPersonReadRepository>(
            MockBehavior.Strict);

        _service = new PersonQueryService(_repository.Object);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PersonQueryService(null!));
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyId_ShouldReturnValidationFailure()
    {
        var result = await _service.GetByIdAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("شناسه شخص نمی‌تواند خالی باشد.", result.Message);
        Assert.Null(result.Value);

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingPerson_ShouldReturnSuccess()
    {
        var person = CreatePerson();

        using var cancellation = new CancellationTokenSource();

        _repository
            .Setup(x => x.GetByIdAsync(
                person.Id,
                cancellation.Token))
            .ReturnsAsync(person);

        var result = await _service.GetByIdAsync(
            person.Id,
            cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.Equal(person, result.Value);

        _repository.Verify(
            x => x.GetByIdAsync(person.Id, cancellation.Token),
            Times.Once);

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingPerson_ShouldReturnNotFound()
    {
        var id = Guid.NewGuid();

        _repository
            .Setup(x => x.GetByIdAsync(
                id,
                CancellationToken.None))
            .ReturnsAsync((PersonResponseDto?)null);

        var result = await _service.GetByIdAsync(id);

        AssertNotFound(result);

        _repository.Verify(
            x => x.GetByIdAsync(id, CancellationToken.None),
            Times.Once);

        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("123")]
    [InlineData("01234567890")]
    [InlineData("abcdefghij")]
    [InlineData("1111111111")]
    [InlineData("0123456788")]
    public async Task GetByNationalCodeAsync_WithInvalidCode_ShouldReturnValidationFailure(
        string? nationalCode)
    {
        var expected = NationalCode.Create(nationalCode);

        var result = await _service.GetByNationalCodeAsync(
            nationalCode!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(expected.Message, result.Message);
        Assert.Null(result.Value);

        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("0123456789")]
    [InlineData(" 0123456789 ")]
    [InlineData("۰۱۲۳۴۵۶۷۸۹")]
    [InlineData("٠١٢٣٤٥٦٧٨٩")]
    [InlineData(" ۰۱۲۳۴۵۶۷۸۹ ")]
    [InlineData("۰1۲3۴5۶7۸9")]
    public async Task GetByNationalCodeAsync_WithValidCode_ShouldQueryUsingNormalizedCode(
        string nationalCode)
    {
        var person = CreatePerson();

        using var cancellation = new CancellationTokenSource();

        _repository
            .Setup(x => x.GetByNationalCodeAsync(
                ValidNationalCode,
                cancellation.Token))
            .ReturnsAsync(person);

        var result = await _service.GetByNationalCodeAsync(
            nationalCode,
            cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.Equal(person, result.Value);

        _repository.Verify(
            x => x.GetByNationalCodeAsync(
                ValidNationalCode,
                cancellation.Token),
            Times.Once);

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByNationalCodeAsync_WithMissingPerson_ShouldReturnNotFound()
    {
        _repository
            .Setup(x => x.GetByNationalCodeAsync(
                ValidNationalCode,
                CancellationToken.None))
            .ReturnsAsync((PersonResponseDto?)null);

        var result = await _service.GetByNationalCodeAsync(
            ValidNationalCode);

        AssertNotFound(result);

        _repository.Verify(
            x => x.GetByNationalCodeAsync(
                ValidNationalCode,
                CancellationToken.None),
            Times.Once);

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAllAsync_WithPersons_ShouldReturnSuccess()
    {
        IReadOnlyList<PersonResponseDto> persons =
            new List<PersonResponseDto>
            {
                CreatePerson(),
                CreatePerson("1234567891")
            };

        using var cancellation = new CancellationTokenSource();

        _repository
            .Setup(x => x.GetAllAsync(cancellation.Token))
            .ReturnsAsync(persons);

        var result = await _service.GetAllAsync(
            cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.NotNull(result.Value);

        Assert.Collection(
            result.Value!,
            person => Assert.Equal(persons[0], person),
            person => Assert.Equal(persons[1], person));

        _repository.Verify(
            x => x.GetAllAsync(cancellation.Token),
            Times.Once);

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAllAsync_WithNoPersons_ShouldReturnSuccessfulEmptyList()
    {
        _repository
            .Setup(x => x.GetAllAsync(CancellationToken.None))
            .ReturnsAsync(Array.Empty<PersonResponseDto>());

        var result = await _service.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!);

        _repository.Verify(
            x => x.GetAllAsync(CancellationToken.None),
            Times.Once);

        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("nationalCode")]
    [InlineData("all")]
    public async Task Queries_WhenRepositoryThrows_ShouldPropagateException(
        string query)
    {
        var id = Guid.NewGuid();
        var expectedException =
            new InvalidOperationException("Storage failure.");

        SetupQueryException(
            query,
            id,
            CancellationToken.None,
            expectedException);

        var actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExecuteQueryAsync(
                    query,
                    id,
                    CancellationToken.None));

        Assert.Same(expectedException, actualException);

        VerifyQueryCall(query, id, CancellationToken.None);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("nationalCode")]
    [InlineData("all")]
    public async Task Queries_WhenRepositoryCancels_ShouldPropagateCancellation(
        string query)
    {
        var id = Guid.NewGuid();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var expectedException =
            new OperationCanceledException(cancellation.Token);

        SetupQueryException(
            query,
            id,
            cancellation.Token,
            expectedException);

        var actualException =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ExecuteQueryAsync(
                    query,
                    id,
                    cancellation.Token));

        Assert.Same(expectedException, actualException);

        Assert.Equal(
            cancellation.Token,
            actualException.CancellationToken);

        VerifyQueryCall(query, id, cancellation.Token);
    }

    private static PersonResponseDto CreatePerson(
        string nationalCode = ValidNationalCode)
    {
        return new PersonResponseDto(
            Guid.NewGuid(),
            "علی",
            "احمدی",
            nationalCode,
            new DateOnly(1995, 5, 20));
    }

    private static void AssertNotFound(
        Result<PersonResponseDto> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal("شخص موردنظر یافت نشد.", result.Message);
        Assert.Null(result.Value);
    }

    private Task ExecuteQueryAsync(
        string query,
        Guid id,
        CancellationToken cancellationToken)
    {
        return query switch
        {
            "id" => _service.GetByIdAsync(
                id,
                cancellationToken),

            "nationalCode" => _service.GetByNationalCodeAsync(
                ValidNationalCode,
                cancellationToken),

            "all" => _service.GetAllAsync(cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };
    }

    private void SetupQueryException(
        string query,
        Guid id,
        CancellationToken cancellationToken,
        Exception exception)
    {
        switch (query)
        {
            case "id":
                _repository
                    .Setup(x => x.GetByIdAsync(
                        id,
                        cancellationToken))
                    .ThrowsAsync(exception);
                break;

            case "nationalCode":
                _repository
                    .Setup(x => x.GetByNationalCodeAsync(
                        ValidNationalCode,
                        cancellationToken))
                    .ThrowsAsync(exception);
                break;

            case "all":
                _repository
                    .Setup(x => x.GetAllAsync(cancellationToken))
                    .ThrowsAsync(exception);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(query));
        }
    }

    private void VerifyQueryCall(
        string query,
        Guid id,
        CancellationToken cancellationToken)
    {
        switch (query)
        {
            case "id":
                _repository.Verify(
                    x => x.GetByIdAsync(id, cancellationToken),
                    Times.Once);
                break;

            case "nationalCode":
                _repository.Verify(
                    x => x.GetByNationalCodeAsync(
                        ValidNationalCode,
                        cancellationToken),
                    Times.Once);
                break;

            case "all":
                _repository.Verify(
                    x => x.GetAllAsync(cancellationToken),
                    Times.Once);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(query));
        }

        _repository.VerifyNoOtherCalls();
    }
}
