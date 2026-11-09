using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Rira.Domain.Common;

namespace Rira.Endpoint.Extensions;

public static class ResultExtensions
{
    public static Empty ToGrpcResponse(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        result.ThrowIfFailure();

        return new Empty();
    }

    public static TResponse ToGrpcResponse<T, TResponse>(
        this Result<T> result,
        Func<T, TResponse> mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);

        result.ThrowIfFailure();

        if (result.Value is null)
        {
            throw new RpcException(
                new Status(
                    StatusCode.Internal,
                    "خطایی در پردازش درخواست رخ داده است."));
        }

        return mapper(result.Value);
    }

    private static void ThrowIfFailure(this Result result)
    {
        if (result.IsSuccess)
            return;

        var statusCode = result.ErrorType switch
        {
            ErrorType.Validation => StatusCode.InvalidArgument,
            ErrorType.NotFound => StatusCode.NotFound,
            ErrorType.Conflict => StatusCode.AlreadyExists,
            ErrorType.ServerError => StatusCode.Internal,
            _ => StatusCode.Internal
        };

        var message = statusCode == StatusCode.Internal
            ? "خطایی در پردازش درخواست رخ داده است."
            : result.Message ?? "درخواست قابل انجام نیست.";

        throw new RpcException(new Status(statusCode, message));
    }
}
