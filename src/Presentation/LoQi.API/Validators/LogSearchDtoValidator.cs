using FluentValidation;
using LoQi.Application.DTOs;
using LoQi.Application.Extensions;

namespace LoQi.API.Validators;

public class LogSearchDtoValidator : AbstractValidator<LogSearchDto>
{
    public LogSearchDtoValidator()
    {
        // UniqueId validation
        RuleFor(x => x.UniqueId)
            .Must(BeValidGuidOrEmpty)
            .WithMessage("UniqueId must be a valid GUID format or empty");

        // SearchText validation
        RuleFor(x => x.SearchText)
            .MaximumLength(500)
            .WithMessage("SearchText cannot exceed 500 characters")
            .Must(NotContainDangerousCharacters)
            .WithMessage("SearchText contains invalid characters")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchText));

        // Date validation
        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("StartDate must be less than or equal to EndDate");
            //.When(x => x.StartDate.HasValue && x.EndDate.HasValue);

        RuleFor(x => x)
            .Must(HaveReasonableDateRange)
            .WithMessage("Date range cannot exceed 365 days");
            //.When(x => x.StartDate.HasValue && x.EndDate.HasValue);

        // RuleFor(x => x.StartDate)
        //     .GreaterThan(DateTimeOffset.Now.AddYears(-5))
        //     .WithMessage("StartDate cannot be more than 5 years in the past")
        //     .When(x => x.StartDate.HasValue);

        // RuleFor(x => x.EndDate)
        //     .LessThanOrEqualTo(DateTimeOffset.Now.AddDays(1))
        //     .WithMessage("EndDate cannot be in the future")
        //     .When(x => x.EndDate.HasValue);

        // Level validation
        RuleFor(x => x.LevelId)
            .InclusiveBetween(0, 5)
            .WithMessage("LevelId must be between 0 (Verbose) and 5 (Fatal)")
            .When(x => x.LevelId.HasValue);

        // Source validation
        RuleFor(x => x.Source)
            .MaximumLength(100)
            .WithMessage("Source cannot exceed 100 characters")
            .Must(NotContainDangerousCharacters)
            .WithMessage("Source contains invalid characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));

        // Pagination validation
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("Page cannot exceed 10000");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000)
            .WithMessage("PageSize must be between 1 and 1000");

        // OrderBy validation
        RuleFor(x => x.OrderBy)
            .NotEmpty()
            .WithMessage("OrderBy is required")
            .Must(BeValidOrderBy)
            .WithMessage("OrderBy must be 'timestamp', 'level', or 'source'");

        // Complex validation: At least one search criteria
        RuleFor(x => x)
            .Must(HaveAtLeastOneSearchCriteria)
            .WithMessage("At least one search criteria must be provided")
            .When(x => string.IsNullOrWhiteSpace(x.UniqueId)); // UniqueId search is exempt
    }

    private static bool BeValidGuidOrEmpty(string? uniqueId)
    {
        return string.IsNullOrWhiteSpace(uniqueId) || Guid.TryParse(uniqueId, out _);
    }

    private static bool NotContainDangerousCharacters(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        
        // Block potential SQL injection attempts and dangerous characters
        var dangerousPatterns = new[] { "--", "/*", "*/", "xp_", "sp_", "exec", "execute", "drop", "delete", "truncate" };
        var lowerText = text.ToLowerInvariant();
        
        return !dangerousPatterns.Any(pattern => lowerText.Contains(pattern));
    }

    private static bool HaveReasonableDateRange(LogSearchDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.StartDate) || !string.IsNullOrWhiteSpace(dto.EndDate))
        {
            return true;
        }
        
        var daysDifference = dto.EndDate.ParseDateTimeOffset() - dto.StartDate.ParseDateTimeOffset();

        var diff = 0.0;
        if (daysDifference is not null)
        {
            diff = daysDifference.Value.TotalDays;
        }
        
        return diff <= 365;
    }

    private static bool BeValidOrderBy(string orderBy)
    {
        var validOrderBy = new[] { "timestamp", "level", "source" };
        return validOrderBy.Contains(orderBy?.ToLowerInvariant());
    }

    private static bool HaveAtLeastOneSearchCriteria(LogSearchDto dto)
    {
        return !string.IsNullOrWhiteSpace(dto.SearchText) ||
               !string.IsNullOrWhiteSpace(dto.StartDate) ||
               !string.IsNullOrWhiteSpace(dto.EndDate) ||
               dto.LevelId.HasValue ||
               !string.IsNullOrWhiteSpace(dto.Source) ||
               !string.IsNullOrWhiteSpace(dto.CorrelationId);
    }
}