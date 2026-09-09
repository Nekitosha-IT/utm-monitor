using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UTMMonitor;

public static class EgaisMarkAnalyzer
{
    static readonly Regex Digits14 = new(@"(?<!\d)\d{14}(?!\d)", RegexOptions.Compiled);
    static readonly Regex GSSeparated = new(@"(?<!\d)\d{3}[\x1D\x1E\x1F\x04]\d{3}[\x1D\x1E\x1F\x04]\d{8}(?!\d)", RegexOptions.Compiled);
    static readonly Regex Any14 = new(@"\d{14}", RegexOptions.Compiled);

    public sealed record ExtractedMark(string Raw, MarkParts Parts, bool Duplicate);
    public sealed record ResponseAnalysis(string Status, string Message, bool IsSuccess, bool IsError, bool IsTimeout, bool IsTransportError);

    public static List<ExtractedMark> ExtractMarks(string raw)
    {
        var result = new List<ExtractedMark>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in Candidates(raw))
        {
            var normalized = Normalize(candidate);
            if (string.IsNullOrWhiteSpace(normalized)) continue;
            var parts = Parse(normalized);
            if (string.IsNullOrWhiteSpace(parts.Type) || string.IsNullOrWhiteSpace(parts.Rank) || string.IsNullOrWhiteSpace(parts.Number)) continue;
            var key = $"{parts.Type}|{parts.Rank}|{parts.Number}";
            var duplicate = !seen.Add(key);
            result.Add(new ExtractedMark(candidate, parts with { Raw = candidate }, duplicate));
        }
        return result;
    }

    public static MarkParts Parse(string raw)
    {
        var digits = Regex.Replace(raw ?? "", @"\D", "");
        return digits.Length >= 14
            ? new(digits[..3], digits.Substring(3, 3), digits.Substring(6, 8), raw)
            : new("", "", "", raw ?? "");
    }

    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Replace("\u001D", "").Replace("\u001E", "").Replace("\u001F", "").Replace("\u0004", "");
        return Regex.Replace(s, @"\s+", "").Trim();
    }

    public static ResponseAnalysis AnalyzeResponse(string raw, bool transportSuccess = true)
    {
        if (!transportSuccess) return new("UTM_ERROR", "Ошибка транспорта/HTTP УТМ", false, true, false, true);
        if (string.IsNullOrWhiteSpace(raw)) return new("UNKNOWN", "Пустой ответ УТМ", false, false, false, false);

        if (TryJson(raw, out var json)) return AnalyzeJson(json);
        if (TryXml(raw, out var xml)) return AnalyzeXml(xml!);

        var text = raw.ToLowerInvariant();
        if (ContainsAny(text, "timeout", "timed out", "таймаут", "истекло время")) return new("TIMEOUT", "Таймаут ответа", false, true, true, false);
        if (ContainsAny(text, "error", "ошиб", "reject", "отказ", "invalid", "не найден", "not found", "fault")) return new("EGAIS_ERROR", ShortMessage(raw), false, true, false, false);
        if (ContainsAny(text, "success", "accepted", "processed", "ok", "принят", "обработан")) return new("OK", "Ответ указывает на успешную обработку", true, false, false, false);
        return new("UNKNOWN", "Неизвестный формат ответа ЕГАИС/УТМ", false, false, false, false);
    }

    static ResponseAnalysis AnalyzeJson(JsonElement root)
    {
        var error = FindValue(root, new[] { "error", "errors", "fault", "exception", "errorCode", "errorMessage", "rejectReason", "rejectDescription" });
        var success = FindValue(root, new[] { "success", "successful", "accepted", "processed", "ok" });
        var status = FindValue(root, new[] { "status", "state", "result", "responseStatus", "code", "resultCode" });
        var message = FindValue(root, new[] { "message", "description", "text", "errorMessage", "reason", "resultMessage" });

        if (!string.IsNullOrWhiteSpace(error) && !IsFalse(error)) return new("EGAIS_ERROR", message ?? error!, false, true, false, false);
        if (IsTrue(success)) return new("OK", message ?? "Успешный ответ", true, false, false, false);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.ToLowerInvariant();
            if (ContainsAny(s, "error", "reject", "fail", "invalid", "ошиб", "отказ", "reject")) return new("EGAIS_ERROR", message ?? status!, false, true, false, false);
            if (ContainsAny(s, "ok", "success", "accepted", "processed", "принят", "обработан", "done", "completed")) return new("OK", message ?? status!, true, false, false, false);
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            var m = message.ToLowerInvariant();
            if (ContainsAny(m, "error", "ошиб", "отказ", "не найден", "invalid", "reject")) return new("EGAIS_ERROR", message!, false, true, false, false);
        }
        return new("UNKNOWN", "Структурированный ответ получен, но результат не распознан", false, false, false, false);
    }

    static ResponseAnalysis AnalyzeXml(XDocument doc)
    {
        var nodes = doc.DescendantsAndSelf();
        var errorNode = nodes.FirstOrDefault(e => ContainsAny(e.Name.LocalName.ToLowerInvariant(), "error", "fault", "reject", "exception"));
        if (errorNode != null)
        {
            var text = string.IsNullOrWhiteSpace(errorNode.Value) ? errorNode.Name.LocalName : errorNode.Value.Trim();
            return new("EGAIS_ERROR", ShortMessage(text), false, true, false, false);
        }
        var statusNode = nodes.FirstOrDefault(e => ContainsAny(e.Name.LocalName.ToLowerInvariant(), "status", "result", "state", "conclusion", "success"));
        var status = statusNode?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.ToLowerInvariant();
            if (ContainsAny(s, "error", "reject", "fail", "invalid", "ошиб", "отказ", "не найден")) return new("EGAIS_ERROR", status!, false, true, false, false);
            if (ContainsAny(s, "ok", "success", "accepted", "processed", "принят", "обработан", "done", "completed")) return new("OK", status!, true, false, false, false);
        }
        var textAll = doc.ToString(SaveOptions.DisableFormatting).ToLowerInvariant();
        if (ContainsAny(textAll, "fault", "errorcode", "errormessage", "rejectreason")) return new("EGAIS_ERROR", "В XML обнаружен блок ошибки ЕГАИС", false, true, false, false);
        return new("UNKNOWN", "XML-ответ получен, но результат не распознан", false, false, false, false);
    }

    static IEnumerable<string> Candidates(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (Match m in GSSeparated.Matches(raw)) yield return m.Value;
        foreach (Match m in Digits14.Matches(raw)) yield return m.Value;

        try
        {
            using var j = JsonDocument.Parse(raw);
            foreach (var s in JsonStrings(j.RootElement))
            {
                foreach (Match m in GSSeparated.Matches(s)) yield return m.Value;
                foreach (Match m in Any14.Matches(s)) yield return m.Value;
            }
        }
        catch { }

        try
        {
            var x = XDocument.Parse(raw);
            foreach (var e in x.Descendants())
            {
                if (!LooksLikeMarkField(e.Name.LocalName)) continue;
                foreach (Match m in GSSeparated.Matches(e.Value)) yield return m.Value;
                foreach (Match m in Any14.Matches(e.Value)) yield return m.Value;
            }
        }
        catch { }
    }

    static bool LooksLikeMarkField(string name) => ContainsAny(name.ToLowerInvariant(), "mark", "barcode", "datamatrix", "excise", "amc", "code");
    static IEnumerable<string> JsonStrings(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String) { yield return e.GetString() ?? ""; yield break; }
        if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) foreach (var s in JsonStrings(x)) yield return s;
        if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) foreach (var s in JsonStrings(p.Value)) yield return s;
    }
    static bool TryJson(string raw, out JsonElement root) { try { using var d = JsonDocument.Parse(raw); root = d.RootElement.Clone(); return true; } catch { root = default; return false; } }
    static bool TryXml(string raw, out XDocument? doc) { try { doc = XDocument.Parse(raw); return true; } catch { doc = null; return false; } }
    static string? FindValue(JsonElement e, string[] keys)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (keys.Any(k => p.Name.Equals(k, StringComparison.OrdinalIgnoreCase))) return p.Value.ToString();
                var nested = FindValue(p.Value, keys); if (nested != null) return nested;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) { var nested = FindValue(x, keys); if (nested != null) return nested; }
        return null;
    }
    static bool IsTrue(string? s) => bool.TryParse(s, out var b) && b || string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase);
    static bool IsFalse(string? s) => bool.TryParse(s, out var b) && !b || string.Equals(s, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase);
    static bool ContainsAny(string s, params string[] values) => values.Any(s.Contains);
    static string ShortMessage(string s) => Regex.Replace(s.Trim(), @"\s+", " ").Length > 500 ? Regex.Replace(s.Trim(), @"\s+", " ")[..500] + "…" : Regex.Replace(s.Trim(), @"\s+", " ");
}
