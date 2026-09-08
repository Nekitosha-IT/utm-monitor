namespace UTMMonitor;

public static class HealthEvaluator
{
    public static IReadOnlyList<HealthNotice> Evaluate(Utm u, UtmStatus? status, IEnumerable<CertificateInfo> certificates)
    {
        var result = new List<HealthNotice>();
        if (!u.Enabled)
        {
            result.Add(new(HealthLevel.Warning,"УТМ отключён",$"{u.Name} отключён в настройках."));
            return result;
        }

        if (status is null)
        {
            result.Add(new(HealthLevel.Warning,"УТМ ещё не проверен",$"Нет результата проверки {u.Name}."));
            return result;
        }

        if (!status.Online)
        {
            result.Add(new(HealthLevel.Error,"УТМ недоступен",$"Не удалось подключиться к {u.Name} ({u.Host}:{u.Port}).",status.Error));
            return result;
        }

        if (status.ResponseMs >= 3000)
            result.Add(new(HealthLevel.Warning,"УТМ отвечает медленно",$"Ответ от {u.Name}: {status.ResponseMs} мс."));

        foreach (var cert in certificates)
        {
            if (!DateTime.TryParse(cert.NotAfter, out var expiry)) continue;
            var days = (expiry - DateTime.Now).TotalDays;
            if (days < 0)
                result.Add(new(HealthLevel.Error,"Сертификат просрочен",$"{cert.Name}: срок действия истёк {expiry:dd.MM.yyyy}."));
            else if (days <= 30)
                result.Add(new(HealthLevel.Warning,"Сертификат скоро истекает",$"{cert.Name}: осталось {Math.Max(0,(int)Math.Ceiling(days))} дн. до {expiry:dd.MM.yyyy}."));
        }

        if (result.Count == 0)
            result.Add(new(HealthLevel.Good,"УТМ работает нормально",$"{u.Name} доступен, ответ {status.ResponseMs} мс."));

        return result;
    }
}
