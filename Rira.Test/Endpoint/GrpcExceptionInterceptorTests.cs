using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Rira.Endpoint.Interceptors;

namespace Rira.Tests.Endpoint;

public class GrpcExceptionInterceptorTests
{
    private readonly Mock<ILogger<GrpcExceptionInterceptor>> _logger = new();
    private GrpcExceptionInterceptor Interceptor => new(_logger.Object);

    [Fact]
    public async Task SuccessfulCall_ReturnsOriginalResponse()
    {
        var request = new Empty();
        var response = new Empty();
        var context = CreateContext();

        var actual = await Interceptor.UnaryServerHandler<Empty, Empty>(
            request, context, (received, callContext) =>
            {
                Assert.Same(request, received);
                Assert.Same(context, callContext);
                return Task.FromResult(response);
            });

        Assert.Same(response, actual);
        _logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RpcException_PreservesStatusDetailAndTrailers()
    {
        var expected = new RpcException(
            new Status(StatusCode.Aborted, "Reload the person."),
            new Metadata { { "error-code", "concurrency-conflict" } });

        var actual = await InvokeFailure(expected, CreateContext());

        Assert.Same(expected, actual);
        Assert.Equal("concurrency-conflict", actual.Trailers.GetValue("error-code"));
        _logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnexpectedException_LogsOriginalErrorAndHidesDetails()
    {
        var expected = new InvalidOperationException("Sensitive database details");

        var actual = await InvokeFailure(expected, CreateContext());

        Assert.Equal(StatusCode.Internal, actual.StatusCode);
        Assert.Equal("خطایی در پردازش درخواست رخ داد.", actual.Status.Detail);
        Assert.Empty(actual.Trailers);
        VerifyErrorLogged(expected);
    }

    [Theory]
    [InlineData(false, StatusCode.Cancelled)]
    [InlineData(true, StatusCode.DeadlineExceeded)]
    public async Task CancelledRequest_ReturnsStatusForItsDeadline(
        bool expired, StatusCode expectedStatus)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Fixed extremes make the test independent of wall-clock timing and sleeps.
        var deadline = expired ? DateTime.MinValue : DateTime.MaxValue;
        var context = CreateContext(cancellation.Token, deadline);

        var actual = await InvokeFailure(
            new OperationCanceledException("Private cancellation details", cancellation.Token), context);

        Assert.Equal(expectedStatus, actual.StatusCode);
        Assert.Equal(expired ? "مهلت اجرای درخواست به پایان رسید." : "درخواست لغو شد.", actual.Status.Detail);
        _logger.Verify(x => x.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public async Task CancellationWithoutCancelledRequest_IsInternalFailure()
    {
        var expected = new OperationCanceledException("Unrelated operation cancelled");

        var actual = await InvokeFailure(expected, CreateContext());

        Assert.Equal(StatusCode.Internal, actual.StatusCode);
        Assert.DoesNotContain(expected.Message, actual.Status.Detail);
        VerifyErrorLogged(expected);
    }

    private Task<RpcException> InvokeFailure(Exception error, ServerCallContext context)
        => Assert.ThrowsAsync<RpcException>(() => Interceptor.UnaryServerHandler<Empty, Empty>(
            new Empty(), context, (_, _) => Task.FromException<Empty>(error)));

    private void VerifyErrorLogged(Exception expected)
    {
        _logger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) =>
                state != null &&
                state.ToString()!.Contains("/rira.persons.v1.PersonService/GetPersonById") &&
                state.ToString()!.Contains("test-trace")),
            expected,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private static ServerCallContext CreateContext(
        CancellationToken token = default, DateTime? deadline = null)
    {
        var context = new Mock<ServerCallContext> { CallBase = true };
        context.Protected().SetupGet<string>("MethodCore")
            .Returns("/rira.persons.v1.PersonService/GetPersonById");
        context.Protected().SetupGet<CancellationToken>("CancellationTokenCore").Returns(token);
        context.Protected().SetupGet<DateTime>("DeadlineCore").Returns(deadline ?? DateTime.MaxValue);
        context.Object.UserState["__HttpContext"] = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace"
        };
        return context.Object;
    }
}
