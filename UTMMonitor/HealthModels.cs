namespace UTMMonitor;

public enum HealthLevel
{
    Good,
    Warning,
    Error
}

public sealed record HealthNotice(
    HealthLevel Level,
    string Title,
    string Message,
    string? Details = null);
