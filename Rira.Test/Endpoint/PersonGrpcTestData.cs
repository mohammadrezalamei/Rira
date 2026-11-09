using Rira.Endpoint.Protos;
using ProtoDate = Rira.Endpoint.Protos.Date;

namespace Rira.Test.Endpoint;

internal static class PersonGrpcTestData
{
    public const string FirstCode = "0012345679";
    public const string SecondCode = "1234567891";

    public static CreatePersonRequest ValidCreate(
        string nationalCode = FirstCode)
    {
        return new CreatePersonRequest
        {
            FirstName = "علی",
            LastName = "احمدی",
            NationalCode = nationalCode,
            BirthDate = new ProtoDate
            {
                Year = 1995,
                Month = 5,
                Day = 21
            }
        };
    }

    public static UpdatePersonRequest ToUpdate(
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

    public static CreatePersonRequest InvalidCreate(string scenario)
    {
        var request = ValidCreate();

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
                var future = DateOnly.FromDateTime(
                    DateTime.UtcNow.AddYears(1));

                request.BirthDate = new ProtoDate
                {
                    Year = future.Year,
                    Month = future.Month,
                    Day = future.Day
                };
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unknown validation scenario.");
        }

        return request;
    }
}
