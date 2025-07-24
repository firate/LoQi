using System.Net;
using System.Text.Json;
using LoQi.API.Exceptions;
using LoQi.API.Extensions;

namespace LoQi.API.Middlewares;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        var response = exception switch
        {
            BaseException baseEx => CreateBaseExceptionResponse(baseEx, correlationId),
            ArgumentException argEx => CreateArgumentExceptionResponse(argEx, correlationId),
            UnauthorizedAccessException => CreateUnauthorizedResponse(correlationId),
            NotImplementedException => CreateNotImplementedResponse(correlationId),
            TimeoutException => CreateTimeoutResponse(correlationId),
            _ => CreateGenericErrorResponse(exception, correlationId)
        };

        // ✅ NULL-SAFE LOGGING with extension method
        LogExceptionSafely(exception, correlationId, context);

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = "application/json";

        var jsonResponse = JsonSerializer.Serialize(response.Body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private ErrorResponse CreateBaseExceptionResponse(BaseException exception, string correlationId)
    {
        return new ErrorResponse
        {
            StatusCode = exception.StatusCode,
            Body = new
            {
                success = false,
                error = exception.Message,
                errorCode = exception.ErrorCode,
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    private ErrorResponse CreateArgumentExceptionResponse(ArgumentException exception, string correlationId)
    {
        return new ErrorResponse
        {
            StatusCode = (int)HttpStatusCode.BadRequest,
            Body = new
            {
                success = false,
                error = exception.Message,
                errorCode = "INVALID_ARGUMENT",
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    private ErrorResponse CreateUnauthorizedResponse(string correlationId)
    {
        return new ErrorResponse
        {
            StatusCode = (int)HttpStatusCode.Unauthorized,
            Body = new
            {
                success = false,
                error = "Authentication required",
                errorCode = "UNAUTHORIZED",
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    private ErrorResponse CreateNotImplementedResponse(string correlationId)
    {
        return new ErrorResponse
        {
            StatusCode = (int)HttpStatusCode.NotImplemented,
            Body = new
            {
                success = false,
                error = "This feature is not yet implemented",
                errorCode = "NOT_IMPLEMENTED",
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    private ErrorResponse CreateTimeoutResponse(string correlationId)
    {
        return new ErrorResponse
        {
            StatusCode = (int)HttpStatusCode.RequestTimeout,
            Body = new
            {
                success = false,
                error = "The request timed out",
                errorCode = "TIMEOUT",
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    private ErrorResponse CreateGenericErrorResponse(Exception exception, string correlationId)
    {
        object errorDetails;

        if (_environment.IsDevelopment())
        {
            errorDetails = new
            {
                success = false,
                error = "An internal error occurred",
                errorCode = "INTERNAL_ERROR",
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow,
                details = new
                {
                    message = exception.Message,
                    stackTrace = exception.StackTrace,
                    innerException = exception.InnerException?.Message
                }
            };
        }
        else
        {
            errorDetails = new
            {
                success = false,
                error = "An internal error occurred",
                errorCode = "INTERNAL_ERROR",
                correlationId = correlationId,
                timestamp = DateTimeOffset.UtcNow
            };
        }

        return new ErrorResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Body = errorDetails
        };
    }

    

    private void LogExceptionSafely(Exception exception, string correlationId, HttpContext? context)
    {
        var logLevel = exception switch
        {
            ArgumentException => LogLevel.Warning,
            BaseException baseEx when baseEx.StatusCode < 500 => LogLevel.Warning,
            BaseException => LogLevel.Error,
            _ => LogLevel.Error
        };

        try
        {
            // ✅ SUPER SAFE - Extension method handles all null checks
            using var scope = _logger.BeginScope(context.CreateSafeLoggingContext(correlationId));

            _logger.Log(logLevel, exception, 
                "Exception occurred: {ExceptionType} - {Message}", 
                exception.GetType().Name, exception.Message);
        }
        catch (Exception loggingException)
        {
            // ✅ Fallback logging if scope creation fails
            try
            {
                _logger.LogError(loggingException, 
                    "Failed to create logging scope for exception: {OriginalException}", 
                    exception.Message);
                
                // Simple log without scope
                _logger.Log(logLevel, exception, 
                    "Exception occurred (no context): {ExceptionType} - {Message}", 
                    exception.GetType().Name, exception.Message);
            }
            catch
            {
                // If even fallback fails, swallow the logging error
                // Don't let logging errors break the application
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext? context)
    {
        const string correlationIdHeaderName = "X-Correlation-ID";
        
        try
        {
            if (context?.Request?.Headers?.TryGetValue(correlationIdHeaderName, out var correlationId) == true)
            {
                var idValue = correlationId.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(idValue))
                    return idValue;
            }

            var newCorrelationId = Guid.NewGuid().ToString();
            
            // Safe header addition
            if (context?.Response?.Headers != null)
            {
                try
                {
                    context?.Response?.Headers?.Add(correlationIdHeaderName, newCorrelationId);
                }
                catch
                {
                    // Ignore header addition errors
                }
            }
            
            return newCorrelationId;
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    private class ErrorResponse
    {
        public int StatusCode { get; set; }
        public object Body { get; set; } = null!;
    }
    
    private static string GetSafeUserAgent(HttpContext? context)
    {
        try
        {
            if (context?.Request?.Headers == null)
                return "unknown";

            var userAgent = context.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetSafeRemoteIP(HttpContext? context)
    {
        try
        {
            if (context?.Connection?.RemoteIpAddress == null)
                return "unknown";

            var ip = context.Connection.RemoteIpAddress.ToString();
            return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetSafeQueryString(HttpContext? context)
    {
        try
        {
            if (context?.Request?.QueryString == null)
                return "";

            var queryString = context.Request.QueryString.ToString();
            return queryString ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string GetSafeHost(HttpContext? context)
    {
        try
        {
            if (context?.Request?.Host == null)
                return "unknown";

            var host = context.Request.Host.ToString();
            return string.IsNullOrWhiteSpace(host) ? "unknown" : host;
        }
        catch
        {
            return "unknown";
        }
    }
}