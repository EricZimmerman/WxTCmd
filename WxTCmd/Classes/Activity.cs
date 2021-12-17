using System;
using ServiceStack.DataAnnotations;

namespace WxTCmd.Classes;

public class Activity
{
    [PrimaryKey] public Guid Id { get; set; }

    public string AppId { get; set; }
    public string PackageIdHash { get; set; }
    public string AppActivityId { get; set; }
    public int ActivityType { get; set; }
    public int ActivityStatus { get; set; }
    public Guid ParentActivityId { get; set; }
    public string Tag { get; set; }
    public string Group { get; set; }
    public string MatchId { get; set; }

    public DateTimeOffset LastModifiedTime { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }
    public byte[] Payload { get; set; }
    public int Priority { get; set; }
    public int IsLocalOnly { get; set; }
    public string PlatformDeviceId { get; set; }
    public string DsdDeviceId { get; set; }
    public DateTimeOffset? CreatedInCloud { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    public DateTimeOffset LastModifiedOnClient { get; set; }
    public string GroupAppActivityId { get; set; }
    public byte[] ClipboardPayload { get; set; }
    public string EnterpriseId { get; set; }
    public byte[] OriginalPayload { get; set; }
    public int UserActionState { get; set; }
    public int IsRead { get; set; }
    public DateTimeOffset? OriginalLastModifiedOnClient { get; set; }
    public string GroupItems { get; set; }
    public DateTimeOffset? LocalExpirationTime { get; set; }
    public int ETag { get; set; }
}

/// <summary>
/// Pulls data from AppId here and in ActivityOperation
/// </summary>
public class AppIdInfo
{
    public string Application { get; set; }
    public string Platform { get; set; }

    public override string ToString()
    {
        return $"Platform: {Platform} App: {Application}";
    }
}