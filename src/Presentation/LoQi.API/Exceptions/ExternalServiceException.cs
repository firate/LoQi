namespace LoQi.API.Exceptions;

public class ExternalServiceException : BaseException
{
    public ExternalServiceException(string serviceName, string message) 
        : base($"External service '{serviceName}' error: {message}", "EXTERNAL_SERVICE_ERROR", 502)
    {
    }
}