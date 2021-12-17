using System;


namespace WxTCmd.Classes;

public class ActivityOperationEntry
{
    public ActivityOperationEntry(string id, int operationOrder, string appId, string executable, int activityType, DateTimeOffset lastModifiedTime, DateTimeOffset expirationTime, string payload, DateTimeOffset createdTime, DateTimeOffset? endTime, DateTimeOffset lastModifiedTimeOnClient, DateTimeOffset operationExpirationTime, string platformDeviceId, int operationType, string devicePlatform, string timeZone, string description, DateTimeOffset startTime, string displayText, string clipboardPayload, string contentInfo)
    {
        Id = id;
        OperationOrder = operationOrder;
        AppId = appId;
        Executable = executable;
        ActivityTypeOrg = activityType;
        ActivityType = (ActivityEntry.ActivityTypes) activityType;
        LastModifiedTime = lastModifiedTime;
        ExpirationTime = expirationTime;
        Payload = payload;
        CreatedTime = createdTime;
        EndTime = endTime;


        if (endTime != null && startTime!=endTime)
        {
            if (endTime.Value.Year > 1970)
            {
                Duration = endTime.Value.Subtract(startTime);
            }
                
        }



        LastModifiedTimeOnClient = lastModifiedTimeOnClient;
        OperationExpirationTime = operationExpirationTime;
        PlatformDeviceId = platformDeviceId;
        OperationType = operationType;
        DevicePlatform = devicePlatform;
        TimeZone = timeZone;
        Description = description;
        StartTime = startTime;
        DisplayText = displayText;
        ClipboardPayload = clipboardPayload;
        ContentInfo = contentInfo;
    }

    public string Id { get; set; }

    public int OperationOrder { get; set; }
    public int OperationType { get; set; }

    public string AppId { get; set; }

    public string Executable { get; set; }
    public string Description { get; set; }

    public DateTimeOffset StartTime { get; set; }
    public string DisplayText { get; set; }
    public string ClipboardPayload { get; set; }
    public string ContentInfo { get; set; }

        
    public string DevicePlatform { get; set; }
    public string TimeZone { get; set; }
        


    public int ActivityTypeOrg { get; set; }
    public ActivityEntry.ActivityTypes ActivityType { get; }

    public TimeSpan? Duration { get; set; }

    public DateTimeOffset LastModifiedTime { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }

    public string Payload { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public DateTimeOffset LastModifiedTimeOnClient { get; set; }
    public DateTimeOffset OperationExpirationTime { get; set; }

    public string PlatformDeviceId { get; set; }

}