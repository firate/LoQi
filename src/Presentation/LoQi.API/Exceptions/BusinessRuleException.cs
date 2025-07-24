namespace LoQi.API.Exceptions;

public class BusinessRuleException : BaseException
{
    public BusinessRuleException(string message) 
        : base(message, "BUSINESS_RULE_VIOLATION", 400)
    {
    }
}