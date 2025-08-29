namespace LoQi.Application.DTOs;

public record AddLogDto
{
    public string Message { get; set; }
    public int LevelId { get; set; }
    public string Source { get; set; }
    public string CorrelationId { get; set; }
    public string? RedisStreamId { get; set; }
}