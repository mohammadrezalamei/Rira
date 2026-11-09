using Grpc.Core;
using Rira.Application.DTOs.Persons;
using Rira.Endpoint.Protos;
using ProtoDate = Rira.Endpoint.Protos.Date;

namespace Rira.Endpoint.Mappings;

public static class PersonGrpcMappings
{
    public static CreatePersonRequestDto ToDto(
        this CreatePersonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreatePersonRequestDto(
            request.FirstName,
            request.LastName,
            request.NationalCode,
            ToDateOnly(request.BirthDate));
    }

    public static UpdatePersonRequestDto ToDto(
        this UpdatePersonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdatePersonRequestDto(
            request.FirstName,
            request.LastName,
            request.NationalCode,
            ToDateOnly(request.BirthDate));
    }

    public static PersonResponse ToGrpcResponse(
        this PersonResponseDto person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new PersonResponse
        {
            Id = person.Id.ToString("D"),
            FirstName = person.FirstName,
            LastName = person.LastName,
            NationalCode = person.NationalCode,
            BirthDate = new ProtoDate
            {
                Year = person.BirthDate.Year,
                Month = person.BirthDate.Month,
                Day = person.BirthDate.Day
            }
        };
    }

    public static GetAllPersonsResponse ToGrpcResponse(
        this IReadOnlyList<PersonResponseDto> persons)
    {
        ArgumentNullException.ThrowIfNull(persons);

        var response = new GetAllPersonsResponse();

        response.Persons.AddRange(
            persons.Select(person => person.ToGrpcResponse()));

        return response;
    }

    private static DateOnly ToDateOnly(ProtoDate? date)
    {
        if (date is null)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "تاریخ تولد الزامی است."));
        }

        try
        {
            return new DateOnly(date.Year, date.Month, date.Day);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "تاریخ تولد معتبر نیست."));
        }
    }
}
