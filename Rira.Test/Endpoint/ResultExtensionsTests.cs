using Grpc.Core;
using Rira.Domain.Common;
using Rira.Endpoint.Extensions;

namespace Rira.Tests.Endpoint;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Conflict, StatusCode.AlreadyExists, false)]
    [InlineData(ErrorType.Conflict, StatusCode.AlreadyExists, true)]
    [InlineData(ErrorType.ConcurrencyConflict, StatusCode.Aborted, false)]
    [InlineData(ErrorType.ConcurrencyConflict, StatusCode.Aborted, true)]
    [InlineData(ErrorType.ServerError, StatusCode.Internal, false)]
    [InlineData(ErrorType.ServerError, StatusCode.Internal, true)]
    public void Failure_MapsStatusAndProtectsInternalDetails(
        ErrorType errorType, StatusCode expectedStatus, bool hasValue)
    {
        const string message = "Original failure details";
        var mapped = false;

        var exception = Assert.Throws<RpcException>(() =>
        {
            if (hasValue)
                Result<string>.Failure(message, errorType).ToGrpcResponse(value =>
                {
                    mapped = true;
                    return value;
                });
            else
                Result.Failure(message, errorType).ToGrpcResponse();
        });

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.False(mapped);
        Assert.Equal(errorType == ErrorType.ServerError
            ? "خطایی در پردازش درخواست رخ داده است."
            : message, exception.Status.Detail);
    }
}
