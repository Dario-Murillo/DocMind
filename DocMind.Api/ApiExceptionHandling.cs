namespace DocMind.Api;

using DocMind.Api.Contracts;
using DocMind.Core.Documents;

// A single place to translate exceptions raised anywhere in the pipeline (including Minimal API
// parameter binding, which runs before an endpoint's own body) into consistent JSON error
// responses. Keeps individual endpoints free of repetitive try/catch blocks.
public static partial class ApiExceptionHandling
{
    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (NoExtractableTextException ex)
            {
                await WriteError(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
            }
            catch (BadHttpRequestException)
            {
                await WriteError(context, StatusCodes.Status400BadRequest, "The request could not be read. Ensure the request body is well-formed.");
            }
            catch (ArgumentException ex)
            {
                await WriteError(context, StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteError(context, StatusCodes.Status503ServiceUnavailable, ex.Message);
            }
            catch (Exception ex)
            {
                LogUnhandledException(app.Logger, ex, context.Request.Method, context.Request.Path);
                await WriteError(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        });

        return app;
    }

    private static Task WriteError(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ErrorResponse(message));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception processing {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string path);
}
