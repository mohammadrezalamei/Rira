using Moq;
using Rira.Application.Contracts.Common;
using Rira.Application.DTOs.Persons;
using Rira.Application.Services.Commands;
using Rira.Domain.Common;
using Rira.Domain.Entities;
using Rira.Domain.Repositories;
using Rira.Domain.ValueObjects;

namespace Rira.Tests.Application.Commands;

public class PersonCommandServiceTests
{
    private readonly Mock<IPersonRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private PersonCommandService Service => new(_repository.Object, _unitOfWork.Object);
    private static readonly DateOnly Birth = new(1995, 5, 20);
    private static CreatePersonRequestDto Request => new(" Ali ", " Ahmadi ", "۰۱۲۳۴۵۶۷۸۹", Birth);
    private static Person Person() => Rira.Domain.Entities.Person.Create(
        "Old", "Name", "0123456789", Birth, new DateOnly(2026, 1, 1)).Value!;

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new PersonCommandService(null!, _unitOfWork.Object));
        Assert.Throws<ArgumentNullException>(() => new PersonCommandService(_repository.Object, null!));
    }

    [Fact]
    public async Task InvalidRequests_DoNotAccessStorage()
    {
        Assert.Equal(ErrorType.Validation, (await Service.CreateAsync(null!)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await Service.CreateAsync(Request with { FirstName = " " })).ErrorType);
        Assert.Equal(ErrorType.Validation, (await Service.CreateAsync(Request with { NationalCode = "123" })).ErrorType);
        Assert.Equal(ErrorType.Validation, (await Service.CreateAsync(Request with { BirthDate = default })).ErrorType);
        Assert.Equal(ErrorType.Validation, (await Service.UpdateAsync(Guid.Empty, new("A", "B", "0123456789", Birth))).ErrorType);
        Assert.Equal(ErrorType.Validation, (await Service.UpdateAsync(Guid.NewGuid(), null!)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await Service.DeleteAsync(Guid.Empty)).ErrorType);
        _repository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task MissingPerson_ReturnsNotFound(string operation)
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.GetByIdAsync(id, default)).ReturnsAsync((Person?)null);
        var result = operation == "delete"
            ? await Service.DeleteAsync(id)
            : await Service.UpdateAsync(id, new("New", "Name", "0123456789", Birth));
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    public async Task DuplicateCode_ReturnsConflictWithoutMutation(string operation)
    {
        var person = Person();
        var excludedId = operation == "update" ? person.Id : (Guid?)null;
        if (operation == "update")
            _repository.Setup(x => x.GetByIdAsync(person.Id, default)).ReturnsAsync(person);
        _repository.Setup(x => x.GetByNationalCodeAsync(It.IsAny<NationalCode>(), excludedId, default))
            .ReturnsAsync(Person());
        var result = operation == "create"
            ? await Service.CreateAsync(Request)
            : await Service.UpdateAsync(person.Id, new("New", "Name", "0123456789", Birth));
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal("Old", person.FirstName);
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidUpdate_DoesNotMutatePersonOrSave()
    {
        var person = Person();
        _repository.Setup(x => x.GetByIdAsync(person.Id, default)).ReturnsAsync(person);
        _repository.Setup(x => x.GetByNationalCodeAsync(It.IsAny<NationalCode>(), person.Id, default))
            .ReturnsAsync((Person?)null);
        var result = await Service.UpdateAsync(person.Id, new("New", "Name", "1234567891", default));
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("Old", person.FirstName);
        Assert.Equal("0123456789", person.NationalCode.Value);
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task SuccessfulCommands_StageAndSaveWithToken(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var person = Person();
        SetupOperation(operation, person, cancellation.Token);
        _unitOfWork.Setup(x => x.SaveChangesAsync(cancellation.Token)).ReturnsAsync(Result.Success());
        var result = await Execute(operation, person.Id, cancellation.Token);
        Assert.True(result.IsSuccess);
        _unitOfWork.Verify(x => x.SaveChangesAsync(cancellation.Token), Times.Once);
        if (operation == "update")
        {
            Assert.Equal("New", person.FirstName);
            Assert.Equal("1234567891", person.NationalCode.Value);
        }
        if (result is Result<PersonResponseDto> response)
        {
            Assert.NotNull(response.Value);
            Assert.NotEqual(Guid.Empty, response.Value!.Id);
            Assert.Equal(operation == "create" ? "Ali" : "New", response.Value.FirstName);
        }
        _unitOfWork.Verify(x => x.DiscardChanges(), Times.Never);
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
    public async Task FailedSave_DiscardsPendingChangesAndRestoresPerson(string operation, string outcome)
    {
        var person = Person();
        var id = person.Id;
        SetupOperation(operation, person, default);
        _unitOfWork.Setup(x => x.DiscardChanges());
        var save = _unitOfWork.Setup(x => x.SaveChangesAsync(default));
        if (outcome == "failure")
        {
            save.ReturnsAsync(Result.Failure("Save failed", ErrorType.ServerError));
            var result = await Execute(operation, id, default);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ServerError, result.ErrorType);
            Assert.Equal("Save failed", result.Message);
        }
        else if (outcome == "exception")
        {
            var error = new InvalidOperationException("Save failed");
            save.ThrowsAsync(error);
            Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(operation, id, default)));
        }
        else
        {
            save.ThrowsAsync(new OperationCanceledException());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Execute(operation, id, default));
        }
        _unitOfWork.Verify(x => x.DiscardChanges(), Times.Once);
        Assert.Equal(id, person.Id);
        Assert.Equal("Old", person.FirstName);
        Assert.Equal("Name", person.LastName);
        Assert.Equal("0123456789", person.NationalCode.Value);
        Assert.Equal(Birth, person.BirthDate.Value);
    }

    [Fact]
    public async Task RepositoryUpdateException_RestoresPersonAndDiscardsChanges()
    {
        var person = Person();
        SetupOperation("update", person, default);
        _repository.Setup(x => x.Update(person)).Throws(new InvalidOperationException());
        _unitOfWork.Setup(x => x.DiscardChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Execute("update", person.Id, default));
        Assert.Equal("Old", person.FirstName);
        Assert.Equal("0123456789", person.NationalCode.Value);
        _unitOfWork.Verify(x => x.DiscardChanges(), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupOperation(string operation, Person person, CancellationToken token)
    {
        if (operation != "create")
            _repository.Setup(x => x.GetByIdAsync(person.Id, token)).ReturnsAsync(person);
        if (operation != "delete")
            _repository.Setup(x => x.GetByNationalCodeAsync(
                It.Is<NationalCode>(n => n.Value == (operation == "create" ? "0123456789" : "1234567891")),
                operation == "create" ? null : person.Id, token)).ReturnsAsync((Person?)null);
        if (operation == "create")
            _repository.Setup(x => x.AddAsync(It.IsAny<Person>(), token)).Returns(Task.CompletedTask);
        else if (operation == "update")
            _repository.Setup(x => x.Update(person));
        else
            _repository.Setup(x => x.Remove(person));
    }

    private async Task<Result> Execute(string operation, Guid id, CancellationToken token)
    {
        return operation switch
        {
            "create" => await Service.CreateAsync(Request, token),
            "update" => await Service.UpdateAsync(id, new("New", "Surname", "1234567891", new DateOnly(2000, 1, 1)), token),
            "delete" => await Service.DeleteAsync(id, token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}
