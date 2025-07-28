namespace LoQi.Application.DTOs;

public record AddLogDto
{
    public string Message { get; set; }
    public int Level { get; set; }
    public string Source { get; set; }
    public string CorrelationId { get; set; }
}