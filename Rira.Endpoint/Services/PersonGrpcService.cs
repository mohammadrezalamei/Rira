using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Rira.Application.Contracts.Services;
using Rira.Endpoint.Extensions;
using Rira.Endpoint.Mappings;
using Rira.Endpoint.Protos;

namespace Rira.Endpoint.Services;

public sealed class PersonGrpcService : PersonService.PersonServiceBase
{
    private readonly IPersonCommandService _commandService;
    private readonly IPersonQueryService _queryService;

    public PersonGrpcService(
        IPersonCommandService commandService,
        IPersonQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(commandService);
        ArgumentNullException.ThrowIfNull(queryService);

        _commandService = commandService;
        _queryService = queryService;
    }

    public override async Task<PersonResponse> CreatePerson(
        CreatePersonRequest request,
        ServerCallContext context)
    {
        var result = await _commandService.CreateAsync(
            request.ToDto(),
            context.CancellationToken);

        return result.ToGrpcResponse(person => person.ToGrpcResponse());
    }

    public override async Task<PersonResponse> UpdatePerson(
        UpdatePersonRequest request,
        ServerCallContext context)
    {
        var id = ParsePersonId(request.Id);

        var result = await _commandService.UpdateAsync(
            id,
            request.ToDto(),
            context.CancellationToken);

        return result.ToGrpcResponse(person => person.ToGrpcResponse());
    }

    public override async Task<Empty> DeletePerson(
        DeletePersonRequest request,
        ServerCallContext context)
    {
        var id = ParsePersonId(request.Id);

        var result = await _commandService.DeleteAsync(
            id,
            context.CancellationToken);

        return result.ToGrpcResponse();
    }

    public override async Task<PersonResponse> GetPersonById(
        GetPersonByIdRequest request,
        ServerCallContext context)
    {
        var id = ParsePersonId(request.Id);

        var result = await _queryService.GetByIdAsync(
            id,
            context.CancellationToken);

        return result.ToGrpcResponse(person => person.ToGrpcResponse());
    }

    public override async Task<PersonResponse> GetPersonByNationalCode(
        GetPersonByNationalCodeRequest request,
        ServerCallContext context)
    {
        var result = await _queryService.GetByNationalCodeAsync(
            request.NationalCode,
            context.CancellationToken);

        return result.ToGrpcResponse(person => person.ToGrpcResponse());
    }

    public override async Task<GetAllPersonsResponse> GetAllPersons(
        Empty request,
        ServerCallContext context)
    {
        var result = await _queryService.GetAllAsync(
            context.CancellationToken);

        return result.ToGrpcResponse(persons => persons.ToGrpcResponse());
    }

    private static Guid ParsePersonId(string value)
    {
        if (!Guid.TryParse(value, out var id))
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "فرمت شناسه شخص معتبر نیست."));
        }

        if (id == Guid.Empty)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "شناسه شخص نمی‌تواند خالی باشد."));
        }

        return id;
    }
}
