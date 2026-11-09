using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Rira.Endpoint.Interceptors;

public sealed class GrpcExceptionInterceptor : Interceptor
{
    private readonly ILogger<GrpcExceptionInterceptor> _logger;

    public GrpcExceptionInterceptor(
        ILogger<GrpcExceptionInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            // Preserve statuses produced by mappings and ResultExtensions.
            throw;
        }
        catch (OperationCanceledException)
            when (context.CancellationToken.IsCancellationRequested)
        {
            var deadlineExceeded = context.Deadline <= DateTime.UtcNow;

            var statusCode = deadlineExceeded
                ? StatusCode.DeadlineExceeded
                : StatusCode.Cancelled;

            var message = deadlineExceeded
                ? "مهلت اجرای درخواست به پایان رسید."
                : "درخواست لغو شد.";

            _logger.LogDebug(
                "gRPC request stopped. Method: {Method}, StatusCode: {StatusCode}",
                context.Method,
                statusCode);

            throw new RpcException(
                new Status(statusCode, message));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception in gRPC method {Method}. TraceIdentifier: {TraceIdentifier}",
                context.Method,
                context.GetHttpContext().TraceIdentifier);

            throw new RpcException(
                new Status(
                    StatusCode.Internal,
                    "خطایی در پردازش درخواست رخ داد."));
        }
    }
}
