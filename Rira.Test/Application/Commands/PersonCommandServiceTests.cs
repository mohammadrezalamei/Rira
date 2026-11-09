using Moq;
using Rira.Application.Contracts.Common;
using Rira.Application.DTOs.Persons;
using Rira.Application.Services.Commands;
using Rira.Domain.Common;
using Rira.Domain.Entities;
using Rira.Domain.Repositories;
using Rira.Domain.ValueObjects;
using Xunit;

namespace Rira.Tests.Application.Commands;

public class PersonCommandServiceTests
{
    private readonly Mock<IPersonRepository> _repository =
        new(MockBehavior.Strict);

    private readonly Mock<IUnitOfWork> _unitOfWork =
        new(MockBehavior.Strict);

    private static readonly DateOnly Birth = new(1995, 5, 20);
    private static readonly DateOnly UpdatedBirth = new(2000, 1, 1);

    private PersonCommandService Service =>
        new(_repository.Object, _unitOfWork.Object);

    private static CreatePersonRequestDto Request =>
        new(" Ali ", " Ahmadi ", "۰۱۲۳۴۵۶۷۸۹", Birth);

    private static Person CreatePerson(
        string nationalCode = "0123456789")
    {
        return Person.Create(
            "Old",
            "Name",
            nationalCode,
            Birth,
            new DateOnly(2026, 1, 1)).Value!;
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PersonCommandService(null!, _unitOfWork.Object));

        Assert.Throws<ArgumentNullException>(() =>
            new PersonCommandService(_repository.Object, null!));
    }

    [Fact]
    public async Task InvalidRequests_DoNotAccessStorage()
    {
        Assert.Equal(
            ErrorType.Validation,
            (await Service.CreateAsync(null!)).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.CreateAsync(
                Request with { FirstName = " " })).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.CreateAsync(
                Request with { LastName = " " })).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.CreateAsync(
                Request with { NationalCode = "123" })).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.CreateAsync(
                Request with { BirthDate = default })).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.UpdateAsync(
                Guid.Empty,
                new("A", "B", "0123456789", Birth))).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.UpdateAsync(
                Guid.NewGuid(),
                null!)).ErrorType);

        Assert.Equal(
            ErrorType.Validation,
            (await Service.DeleteAsync(Guid.Empty)).ErrorType);

        _repository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task MissingPerson_ReturnsNotFound(string operation)
    {
        var id = Guid.NewGuid();

        _repository
            .Setup(x => x.GetByIdAsync(id, default))
            .ReturnsAsync((Person?)null);

        var result = await Execute(operation, id, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal("شخص موردنظر یافت نشد.", result.Message);

        _repository.Verify(
            x => x.GetByIdAsync(id, default),
            Times.Once);

        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    public async Task DuplicateCode_ReturnsConflictWithoutMutation(
        string operation)
    {
        var person = CreatePerson();

        Guid? excludedId = operation == "update"
            ? person.Id
            : null;

        var nationalCode = operation == "create"
            ? "0123456789"
            : "1234567891";

        var existingPerson = CreatePerson(nationalCode);

        Assert.NotEqual(person.Id, existingPerson.Id);

        if (operation == "update")
        {
            _repository
                .Setup(x => x.GetByIdAsync(person.Id, default))
                .ReturnsAsync(person);
        }

        _repository
            .Setup(x => x.GetByNationalCodeAsync(
                It.Is<NationalCode>(n => n.Value == nationalCode),
                excludedId,
                default))
            .ReturnsAsync(existingPerson);

        var result = await Execute(operation, person.Id, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(
            "شخصی با این کد ملی قبلاً ثبت شده است.",
            result.Message);

        AssertOriginalPerson(person);

        VerifyLookups(operation, person.Id, default);
        VerifyNoStaging();

        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidNationalCodeUpdate_DoesNotCheckDuplicatesOrSave()
    {
        var person = CreatePerson();

        _repository
            .Setup(x => x.GetByIdAsync(person.Id, default))
            .ReturnsAsync(person);

        var result = await Service.UpdateAsync(
            person.Id,
            new("New", "Surname", "123", UpdatedBirth));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Null(result.Value);

        AssertOriginalPerson(person);

        _repository.Verify(
            x => x.GetByIdAsync(person.Id, default),
            Times.Once);

        _repository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidUpdate_DoesNotMutatePersonOrSave()
    {
        var person = CreatePerson();

        _repository
            .Setup(x => x.GetByIdAsync(person.Id, default))
            .ReturnsAsync(person);

        _repository
            .Setup(x => x.GetByNationalCodeAsync(
                It.Is<NationalCode>(n => n.Value == "1234567891"),
                person.Id,
                default))
            .ReturnsAsync((Person?)null);

        var result = await Service.UpdateAsync(
            person.Id,
            new("New", "Surname", "1234567891", default));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Null(result.Value);

        AssertOriginalPerson(person);

        VerifyLookups("update", person.Id, default);
        VerifyNoStaging();

        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_StagesNormalizedPersonAndReturnsSavedValues()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        _repository
            .Setup(x => x.GetByNationalCodeAsync(
                It.Is<NationalCode>(n => n.Value == "0123456789"),
                null,
                token))
            .ReturnsAsync((Person?)null);

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Person>(), token))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(token))
            .ReturnsAsync(Result.Success());

        var result = await Service.CreateAsync(Request, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);

        var dto = Assert.IsType<PersonResponseDto>(result.Value);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Ali", dto.FirstName);
        Assert.Equal("Ahmadi", dto.LastName);
        Assert.Equal("0123456789", dto.NationalCode);
        Assert.Equal(Birth, dto.BirthDate);

        VerifyLookups("create", Guid.Empty, token);

        _repository.Verify(
            x => x.AddAsync(
                It.Is<Person>(person =>
                    person.Id == dto.Id &&
                    person.FirstName == dto.FirstName &&
                    person.LastName == dto.LastName &&
                    person.NationalCode.Value == dto.NationalCode &&
                    person.BirthDate.Value == dto.BirthDate),
                token),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(token),
            Times.Once);

        _unitOfWork.Verify(
            x => x.DiscardChanges(),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_StagesUpdatedPersonAndReturnsSavedValues()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var person = CreatePerson();

        SetupOperation("update", person, token);

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(token))
            .ReturnsAsync(Result.Success());

        var request = new UpdatePersonRequestDto(
            "New",
            "Surname",
            "1234567891",
            UpdatedBirth);

        var result = await Service.UpdateAsync(
            person.Id,
            request,
            token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);

        var dto = Assert.IsType<PersonResponseDto>(result.Value);

        Assert.Equal(person.Id, dto.Id);
        Assert.Equal("New", dto.FirstName);
        Assert.Equal("Surname", dto.LastName);
        Assert.Equal("1234567891", dto.NationalCode);
        Assert.Equal(UpdatedBirth, dto.BirthDate);

        Assert.Equal(dto.FirstName, person.FirstName);
        Assert.Equal(dto.LastName, person.LastName);
        Assert.Equal(dto.NationalCode, person.NationalCode.Value);
        Assert.Equal(dto.BirthDate, person.BirthDate.Value);

        VerifyLookups("update", person.Id, token);

        _repository.Verify(
            x => x.Update(person),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(token),
            Times.Once);

        _unitOfWork.Verify(
            x => x.DiscardChanges(),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_StagesRemovalAndReturnsSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var person = CreatePerson();

        SetupOperation("delete", person, token);

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(token))
            .ReturnsAsync(Result.Success());

        var result = await Service.DeleteAsync(person.Id, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Null(result.Message);

        VerifyLookups("delete", person.Id, token);

        _repository.Verify(
            x => x.Remove(person),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(token),
            Times.Once);

        _unitOfWork.Verify(
            x => x.DiscardChanges(),
            Times.Never);
    }

    [Theory]
    [InlineData("create", "failure")]
    [InlineData("update", "failure")]
    [InlineData("delete", "failure")]
    [InlineData("create", "exception")]
    [InlineData("update", "exception")]
    [InlineData("delete", "exception")]
    [InlineData("create", "cancel")]
    [InlineData("update", "cancel")]
    [InlineData("delete", "cancel")]
    public async Task FailedSave_PropagatesOutcomeWithoutRepeatingUnitOfWorkCleanup(
        string operation,
        string outcome)
    {
        using var cancellation = new CancellationTokenSource();
        var person = CreatePerson();
        var token = cancellation.Token;

        SetupOperation(operation, person, token);

        var save = _unitOfWork.Setup(
            x => x.SaveChangesAsync(token));

        if (outcome == "failure")
        {
            save.ReturnsAsync(
                Result.Failure("Save failed", ErrorType.ServerError));

            var result = await Execute(operation, person.Id, token);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ServerError, result.ErrorType);
            Assert.Equal("Save failed", result.Message);

            if (operation != "delete")
            {
                var response =
                    Assert.IsType<Result<PersonResponseDto>>(result);

                Assert.Null(response.Value);
            }
        }
        else if (outcome == "exception")
        {
            var expected = new InvalidOperationException("Save failed");

            save.ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Execute(operation, person.Id, token));

            Assert.Same(expected, actual);
        }
        else
        {
            var expected = new OperationCanceledException(token);

            save.ThrowsAsync(expected);

            var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Execute(operation, person.Id, token));

            Assert.Same(expected, actual);
            Assert.Equal(token, actual.CancellationToken);
        }

        VerifyLookups(operation, person.Id, token);
        VerifyStaging(operation, person, token);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(token),
            Times.Once);

        _unitOfWork.Verify(
            x => x.DiscardChanges(),
            Times.Never);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task RepositoryStagingException_RequestsCleanupWithoutSaving(
        string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var person = CreatePerson();
        var token = cancellation.Token;
        var expected = new InvalidOperationException("Staging failed");

        SetupOperation(operation, person, token);

        if (operation == "create")
        {
            _repository
                .Setup(x => x.AddAsync(It.IsAny<Person>(), token))
                .ThrowsAsync(expected);
        }
        else if (operation == "update")
        {
            _repository
                .Setup(x => x.Update(person))
                .Throws(expected);
        }
        else
        {
            _repository
                .Setup(x => x.Remove(person))
                .Throws(expected);
        }

        _unitOfWork.Setup(x => x.DiscardChanges());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Execute(operation, person.Id, token));

        Assert.Same(expected, actual);

        VerifyLookups(operation, person.Id, token);
        VerifyStaging(operation, person, token);

        _unitOfWork.Verify(
            x => x.DiscardChanges(),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task CancellationBeforeStaging_RequestsCleanupWithoutSaving(
        string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var person = CreatePerson();
        var token = cancellation.Token;

        if (operation != "create")
        {
            _repository
                .Setup(x => x.GetByIdAsync(person.Id, token))
                .ReturnsAsync(() =>
                {
                    if (operation == "delete")
                        cancellation.Cancel();

                    return person;
                });
        }

        if (operation != "delete")
        {
            var nationalCode = operation == "create"
                ? "0123456789"
                : "1234567891";

            Guid? excludedId = operation == "create"
                ? null
                : person.Id;

            _repository
                .Setup(x => x.GetByNationalCodeAsync(
                    It.Is<NationalCode>(n => n.Value == nationalCode),
                    excludedId,
                    token))
                .ReturnsAsync(() =>
                {
                    cancellation.Cancel();
                    return (Person?)null;
                });
        }

        _unitOfWork.Setup(x => x.DiscardChanges());

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Execute(operation, person.Id, token));

        Assert.Equal(token, exception.CancellationToken);

        VerifyLookups(operation, person.Id, token);
        VerifyNoStaging();

        _unitOfWork.Verify(
            x => x.DiscardChanges(),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupOperation(
        string operation,
        Person person,
        CancellationToken token)
    {
        if (operation != "create")
        {
            _repository
                .Setup(x => x.GetByIdAsync(person.Id, token))
                .ReturnsAsync(person);
        }

        if (operation != "delete")
        {
            var nationalCode = operation == "create"
                ? "0123456789"
                : "1234567891";

            Guid? excludedId = operation == "create"
                ? null
                : person.Id;

            _repository
                .Setup(x => x.GetByNationalCodeAsync(
                    It.Is<NationalCode>(n => n.Value == nationalCode),
                    excludedId,
                    token))
                .ReturnsAsync((Person?)null);
        }

        if (operation == "create")
        {
            _repository
                .Setup(x => x.AddAsync(It.IsAny<Person>(), token))
                .Returns(Task.CompletedTask);
        }
        else if (operation == "update")
        {
            _repository.Setup(x => x.Update(person));
        }
        else
        {
            _repository.Setup(x => x.Remove(person));
        }
    }

    private void VerifyLookups(
        string operation,
        Guid id,
        CancellationToken token)
    {
        if (operation != "create")
        {
            _repository.Verify(
                x => x.GetByIdAsync(id, token),
                Times.Once);
        }

        if (operation != "delete")
        {
            var nationalCode = operation == "create"
                ? "0123456789"
                : "1234567891";

            Guid? excludedId = operation == "create"
                ? null
                : id;

            _repository.Verify(
                x => x.GetByNationalCodeAsync(
                    It.Is<NationalCode>(n => n.Value == nationalCode),
                    excludedId,
                    token),
                Times.Once);
        }
    }

    private void VerifyStaging(
        string operation,
        Person person,
        CancellationToken token)
    {
        if (operation == "create")
        {
            _repository.Verify(
                x => x.AddAsync(It.IsAny<Person>(), token),
                Times.Once);
        }
        else if (operation == "update")
        {
            _repository.Verify(
                x => x.Update(person),
                Times.Once);
        }
        else
        {
            _repository.Verify(
                x => x.Remove(person),
                Times.Once);
        }
    }

    private void VerifyNoStaging()
    {
        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<Person>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _repository.Verify(
            x => x.Update(It.IsAny<Person>()),
            Times.Never);

        _repository.Verify(
            x => x.Remove(It.IsAny<Person>()),
            Times.Never);
    }

    private static void AssertOriginalPerson(Person person)
    {
        Assert.Equal("Old", person.FirstName);
        Assert.Equal("Name", person.LastName);
        Assert.Equal("0123456789", person.NationalCode.Value);
        Assert.Equal(Birth, person.BirthDate.Value);
    }

    private async Task<Result> Execute(
        string operation,
        Guid id,
        CancellationToken token)
    {
        return operation switch
        {
            "create" => await Service.CreateAsync(Request, token),

            "update" => await Service.UpdateAsync(
                id,
                new("New", "Surname", "1234567891", UpdatedBirth),
                token),

            "delete" => await Service.DeleteAsync(id, token),

            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}
