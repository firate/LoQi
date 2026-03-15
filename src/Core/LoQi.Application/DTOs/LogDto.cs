namespace LoQi.Application.DTOs;

public record LogDto
{
    public string? UniqueId { get; init; } 
    public string? CorrelationId { get; init; }
    public string? RedisStreamId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public int LevelId { get; init; }
    public DateTimeOffset Date { get; init; }
}