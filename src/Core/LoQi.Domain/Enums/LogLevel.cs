namespace LoQi.Domain.Enums;

public enum LogLevel
{
    Verbose = 0,      // En detaylı - sadece development
    Debug = 1,        // Debug bilgileri - development/staging  
    Information = 2,  // Normal işlem bilgileri - production
    Warning = 3,      // Uyarılar - production
    Error = 4,        // Hatalar - production
    Fatal = 5         // Kritik hatalar - production
}

// String mapping
public static class LogLevelNames
{
    public static readonly Dictionary<int, string> Names = new()
    {
        { 0, "Verbose" },
        { 1, "Debug" },
        { 2, "Information" },
        { 3, "Warning" },
        { 4, "Error" },
        { 5, "Fatal" }
    };
    
    public static readonly Dictionary<string, int> Values = new()
    {
        { "Verbose", 0 },
        { "Debug", 1 },
        { "Information", 2 },
        { "Warning", 3 },
        { "Error", 4 },
        { "Fatal", 5 }
    };
}