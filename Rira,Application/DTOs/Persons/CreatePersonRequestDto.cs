namespace Rira.Application.DTOs.Persons;

public record CreatePersonRequestDto(
    string FirstName,
    string LastName,
    string NationalCode,
    DateOnly BirthDate);
