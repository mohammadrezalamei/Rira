namespace Rira.Application.DTOs.Persons;

public record PersonResponseDto(
    Guid Id,
    string FirstName,
    string LastName,
    string NationalCode,
    DateOnly BirthDate);
