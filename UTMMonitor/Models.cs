namespace UTMMonitor;

public sealed record Utm(int Id,string Name,string Host,int Port,bool Enabled);
public sealed record UtmStatus(bool Online,int HttpCode,long ResponseMs,string? Version,string? Contour,string? OwnerId,bool? License,string? Error,string? RsaExpiry,string? GostExpiry);
public sealed record CertificateInfo(string Name,string Subject,string Issuer,string NotAfter,bool Valid,string Raw);
public sealed record DocumentRow(string Id,string Type,string Direction,string Number,string Date,string Status,int Items,string RawJson);
public sealed record MarkParts(string Type,string Rank,string Number,string Raw);
public sealed record QueryResult(bool Success,string Message,string? Ticket,string Raw);
public sealed record SyncResult(int Incoming,int Outgoing,int Stored,string Message);
public sealed record MarkCheckResult(bool Success,string Message,string Raw);
public sealed record DocumentItem(string Id,string Name,string Product,string Quantity,string Price,string Mark,string Raw);
public sealed record TicketResult(bool Success,string Ticket,string Status,string Raw);
