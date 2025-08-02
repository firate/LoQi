namespace LoQi.API.Dtos;

public record LogMetadataDto
{
    public List<LogLevelDto> LogLevels { get; init; } = [];
    public List<OrderByOptionDto> OrderByOptions { get; init; } = [];
    public List<PageSizeOptionDto> PageSizeOptions { get; init; } = [];
    public List<SortOrderOptionDto> SortOrderOptions { get; init; } = [];
}

public record LogLevelDto(int Value, string Label, string Color);
public record OrderByOptionDto(string Value, string Label);
public record PageSizeOptionDto(int Value, string Label);
public record SortOrderOptionDto(string Value, string Label, bool IsDescending);