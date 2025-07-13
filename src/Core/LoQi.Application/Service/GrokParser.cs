using System.Text.RegularExpressions;

namespace LoQi.Application.Service;


public class GrokParser
{
    private readonly Dictionary<string, string> _patterns;
    
    public GrokParser()
    {
        _patterns = new Dictionary<string, string>
        {
            ["IP"] = @"\d+\.\d+\.\d+\.\d+",
            ["WORD"] = @"\w+",
            ["INT"] = @"\d+",
            ["TIMESTAMP"] = @"\d{2}/\w{3}/\d{4}:\d{2}:\d{2}:\d{2} [+-]\d{4}",
            ["PATH"] = @"/[^\s]*",
            ["LOGLEVEL"] = @"(DEBUG|INFO|WARN|ERROR|FATAL)",
            ["GREEDYDATA"] = @".*"
        };
    }
    
    public Dictionary<string, string> Parse(string grokPattern, string logLine)
    {
        // Convert grok to regex: %{IP:client_ip} → (?<client_ip>\d+\.\d+\.\d+\.\d+)
        var regex = ConvertGrokToRegex(grokPattern);
        var match = Regex.Match(logLine, regex);
        
        var result = new Dictionary<string, string>();
        foreach (string groupName in match.Groups.Keys)
        {
            if (groupName != "0") // Skip full match
                result[groupName] = match.Groups[groupName].Value;
        }
        return result;
    }
    
    private string ConvertGrokToRegex(string grokPattern)
    {
        // %{IP:client_ip} → (?<client_ip>\d+\.\d+\.\d+\.\d+)
        return Regex.Replace(grokPattern, @"%\{(\w+):(\w+)\}", match =>
        {
            var patternName = match.Groups[1].Value;
            var fieldName = match.Groups[2].Value;
            var regexPattern = _patterns[patternName];
            return $"(?<{fieldName}>{regexPattern})";
        });
    }
}