using System;

namespace WxTCmd.Classes;

/// <summary>A row of the ActivityOperation table (activities waiting to be uploaded)</summary>
public class ActivityOperationEntry
{
    public string Id { get; set; }

    public int OperationOrder { get; set; }
    public int OperationType { get; set; }

    public string AppId { get; set; }

    public string Executable { get; set; }
    public string Description { get; set; }

    public DateTimeOffset? StartTime { get; set; }
    public string DisplayText { get; set; }
    public string ClipboardPayload { get; set; }
    public string ContentInfo { get; set; }

    public string DevicePlatform { get; set; }
    public string TimeZone { get; set; }

    public int ActivityTypeOrg { get; set; }
    public ActivityEntry.ActivityTypes ActivityType => (ActivityEntry.ActivityTypes)ActivityTypeOrg;

    public TimeSpan? Duration => Durations.Between(StartTime, EndTime);

    public DateTimeOffset? LastModifiedTime { get; set; }
    public DateTimeOffset? ExpirationTime { get; set; }

    public string Payload { get; set; }

    public DateTimeOffset? CreatedTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public DateTimeOffset? LastModifiedTimeOnClient { get; set; }
    public DateTimeOffset? OperationExpirationTime { get; set; }

    public string PlatformDeviceId { get; set; }

    public string AppActivityId { get; set; }

    /// <summary>The text in ClipboardPayload (base64 in it), for text formats</summary>
    public string ClipboardText { get; set; }

    public override string ToString()
    {
        return $"Operation Order: {OperationOrder} AppId: {AppId} Expire date: {ExpirationTime}";
    }
}
