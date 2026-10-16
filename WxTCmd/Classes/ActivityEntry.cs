using System;

namespace WxTCmd.Classes;

/// <summary>A row of the Activity table</summary>
public class ActivityEntry
{
    public enum ActivityTypes
    {
        ToastNotification = 2,
        ExecuteOpen = 5,
        InFocus = 6,
        CloudClipboard = 10,
        CopyPaste = 16
    }

    public string Id { get; set; }
    public string Executable { get; set; }
    public string DisplayText { get; set; }
    public string Payload { get; set; }
    public string ClipboardPayload { get; set; }
    public string ContentInfo { get; set; }
    public DateTimeOffset? LastModifiedTime { get; set; }
    public DateTimeOffset? ExpirationTime { get; set; }
    public DateTimeOffset? CreatedInCloud { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    public TimeSpan? Duration => Durations.Between(StartTime, EndTime);

    public DateTimeOffset? LastModifiedOnClient { get; set; }
    public DateTimeOffset? OriginalLastModifiedOnClient { get; set; }

    public int ActivityTypeOrg { get; set; }
    public ActivityTypes ActivityType => (ActivityTypes)ActivityTypeOrg;

    public bool IsLocalOnly { get; set; }

    public int ETag { get; set; }

    public string PackageIdHash { get; set; }

    public string PlatformDeviceId { get; set; }

    public string DevicePlatform { get; set; }
    public string TimeZone { get; set; }

    public string AppActivityId { get; set; }

    /// <summary>The text in ClipboardPayload (base64 in it), for text formats</summary>
    public string ClipboardText { get; set; }

    public override string ToString()
    {
        return $"Exe: {Executable} DisplayText: {DisplayText} Start: {StartTime}";
    }
}

internal static class Durations
{
    /// <summary>EndTime - StartTime, when there is an end time that differs from the start</summary>
    public static TimeSpan? Between(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start == null || end == null || start == end || end.Value.Year <= 1970)
        {
            return null;
        }

        return end.Value - start.Value;
    }
}
