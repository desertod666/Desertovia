namespace Desertod.Domain.Notifications;

[Flags]
public enum NotificationCategory { Work=1, Building=2, Farm=4, Adventure=8, Danger=16, Civic=32, All=63 }

/// <summary>Account choices for browser alerts. Device permission and other delivery channels remain separate.</summary>
public sealed class NotificationPreference
{
    public Guid AccountId { get; set; }
    public long Revision { get; set; }
    public bool BrowserEnabled { get; set; }
    public NotificationCategory Categories { get; set; } = NotificationCategory.All;
    public bool QuietEnabled { get; set; }
    public int QuietStartMinute { get; set; } = 22*60;
    public int QuietEndMinute { get; set; } = 8*60;
    public string TimeZoneId { get; set; } = "UTC";
    public DateTimeOffset? ConsentedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class NotificationPreferenceReceipt
{
    public Guid AccountId { get; set; }
    public Guid RequestId { get; set; }
    public required string Fingerprint { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class NotificationPreferenceRules
{
    public static void Validate(NotificationCategory categories,bool quiet,int start,int end,string zone)
    {
        if ((categories & ~NotificationCategory.All)!=0 || start is <0 or >=1440 || end is <0 or >=1440 || quiet&&start==end)
            throw new GameRuleException("notifications.settings","Choose valid events and two different quiet-hour times.");
        // Browser Intl and the server share an IANA identifier, not a machine-specific Windows name.
        if (string.IsNullOrWhiteSpace(zone) || zone.Length>64 || zone!="UTC"&&!zone.Contains('/'))
            throw new GameRuleException("notifications.timeZone","Choose a valid time zone such as Europe/Oslo.");
        try { _=TimeZoneInfo.FindSystemTimeZoneById(zone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new GameRuleException("notifications.timeZone","Choose a valid time zone such as Europe/Oslo."); }
    }

    public static bool IsQuiet(NotificationPreference settings,DateTimeOffset instant)
    {
        if(!settings.QuietEnabled)return false;
        var local=TimeZoneInfo.ConvertTime(instant,TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId));
        var minute=local.Hour*60+local.Minute;
        return settings.QuietStartMinute<settings.QuietEndMinute
            ? minute>=settings.QuietStartMinute&&minute<settings.QuietEndMinute
            : minute>=settings.QuietStartMinute||minute<settings.QuietEndMinute;
    }
}
