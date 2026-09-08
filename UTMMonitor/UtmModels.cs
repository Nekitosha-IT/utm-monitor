namespace UTMMonitor;

public sealed record Utm(int Id,string Name,string Host,int Port,bool Enabled);
public sealed record UtmStatus(bool Online,int HttpCode,long ResponseMs,string? Version,string? Contour,string? OwnerId,bool? License,string? Error,string? RsaExpiry,string? GostExpiry);
public sealed record CertificateInfo(string Name,string? Subject,string? Issuer,string? NotAfter,bool Valid,string RawJson);
public sealed record DocumentRow(string Id,string Type,string Direction,string Number,string Date,string Status,int Items,string RawJson);
public sealed record DocumentDetails(string Id,string Type,string Direction,string Number,string Date,string Status,string Sender,string Receiver,string OwnerId,int Items,string Raw);
public sealed record DocumentItemRow(string Key,string Name,double Quantity,double Price,string Mark,string Raw);
public sealed record MarkParts(string Type,string Rank,string Number,string Raw);
public sealed record MarkRow(int Id,string DocumentId,string Raw,string Type,string Rank,string Number,string Status,string CheckedAt,string Response);
public sealed record HistoryRow(string CheckedAt,bool Online,int HttpCode,long ResponseMs,string Version,string Error);
public sealed record EventRow(string CreatedAt,string Level,string Message,string Raw);
public sealed record QueryResult(bool Success,string Message,string? Ticket,string Raw);
public sealed record SyncResult(int Incoming,int Outgoing,int Stored,string Message);
public sealed record TicketResult(bool Success,string Ticket,string Status,string Raw);

public enum HealthLevel { Good, Warning, Error }
public sealed record HealthNotice(HealthLevel Level,string Title,string Message,string? Details = null);
