namespace LoQi.API.Extensions;

public static class HttpContextLoggingExtensions
{
    public static Dictionary<string, object> CreateSafeLoggingContext(
        this HttpContext? context, 
        string? correlationId = null)
    {
        return new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString(),
            ["RequestPath"] = context.GetSafeRequestPath(),
            ["RequestMethod"] = context.GetSafeRequestMethod(),
            ["UserAgent"] = context.GetSafeUserAgent(),
            ["RemoteIP"] = context.GetSafeRemoteIP(),
            ["RequestId"] = context?.TraceIdentifier ?? "unknown",
            ["ContentType"] = context.GetSafeContentType(),
            ["QueryString"] = context.GetSafeQueryString(),
            ["RequestSize"] = context?.Request?.ContentLength ?? 0,
            ["Scheme"] = context?.Request?.Scheme ?? "unknown",
            ["Host"] = context.GetSafeHost(),
            ["IsHttps"] = context?.Request?.IsHttps ?? false,
            ["Protocol"] = context?.Request?.Protocol ?? "unknown"
        };
    }

    public static string GetSafeRequestPath(this HttpContext? context)
    {
        try
        {
            return context?.Request?.Path.Value ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public static string GetSafeRequestMethod(this HttpContext? context)
    {
        try
        {
            var method = context?.Request?.Method;
            return string.IsNullOrWhiteSpace(method) ? "unknown" : method;
        }
        catch
        {
            return "unknown";
        }
    }

    public static string GetSafeUserAgent(this HttpContext? context)
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

    public static string GetSafeRemoteIP(this HttpContext? context)
    {
        try
        {
            // Try X-Forwarded-For first (for load balancers/proxies)
            var forwardedFor = context?.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                // Take the first IP if multiple are present
                var firstIP = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIP))
                    return firstIP;
            }

            // Try X-Real-IP header
            var realIP = context?.Request?.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIP))
                return realIP;

            // Fall back to RemoteIpAddress
            var remoteIP = context?.Connection?.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(remoteIP) ? "unknown" : remoteIP;
        }
        catch
        {
            return "unknown";
        }
    }

    public static string GetSafeContentType(this HttpContext? context)
    {
        try
        {
            var contentType = context?.Request?.ContentType;
            return string.IsNullOrWhiteSpace(contentType) ? "unknown" : contentType;
        }
        catch
        {
            return "unknown";
        }
    }

    public static string GetSafeQueryString(this HttpContext? context)
    {
        try
        {
            var queryString = context?.Request?.QueryString.ToString();
            return queryString ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static string GetSafeHost(this HttpContext? context)
    {
        try
        {
            var host = context?.Request?.Host.ToString();
            return string.IsNullOrWhiteSpace(host) ? "unknown" : host;
        }
        catch
        {
            return "unknown";
        }
    }
}