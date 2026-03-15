namespace LoQi.Application.DTOs;

public record AddLogDto
{
    public string Message { get; init; } = string.Empty;
    public int LevelId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string? RedisStreamId { get; init; }
}