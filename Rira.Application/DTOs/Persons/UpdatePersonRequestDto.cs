namespace Rira.Application.DTOs.Persons;

public record UpdatePersonRequestDto(
    string FirstName,
    string LastName,
    string NationalCode,
    DateOnly BirthDate);
