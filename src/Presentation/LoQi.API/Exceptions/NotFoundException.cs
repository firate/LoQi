namespace LoQi.API.Exceptions;

public class NotFoundException : BaseException
{
    public NotFoundException(string resourceName, object key) 
        : base($"{resourceName} with identifier '{key}' was not found.", "RESOURCE_NOT_FOUND", 404)
    {
    }
}