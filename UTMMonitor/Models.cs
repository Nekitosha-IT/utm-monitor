namespace UTMMonitor;

public sealed record Utm(int Id, string Name, string Host, int Port, bool Enabled);
public sealed record UtmStatus(bool Online, int HttpCode, long ResponseMs, string? Version, string? Contour, string? OwnerId, bool? License, string? Error, string? RsaExpiry, string? GostExpiry);
