namespace LoQi.Infrastructure.Models;

/// <summary>
/// Log processing status for Redis Stream routing
/// </summary>
public enum LogProcessingStatus
{
    New,        // Added as raw string
    Success,    // Successfully parsed LogDto
    Failed,     // Parse error, needs manual review
    Retry       // Transient error, can retry
}