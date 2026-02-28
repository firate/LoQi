namespace LoQi.Application.DTOs;

/// <summary>
/// Log sample for quick preview
/// </summary>
public record LogSampleDto
{
    public string UniqueId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public int LevelId { get; init; }
    public DateTimeOffset Date { get; init; }
}