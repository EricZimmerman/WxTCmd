using System;

namespace WxTCmd.Classes
{
    public class ActivityEntry
    {
        public ActivityEntry(string id, string executable, string displayText, string contentInfo,
            DateTimeOffset lastModifiedTime, DateTimeOffset expirationTime, DateTimeOffset? createdInCloud,
            DateTimeOffset startTime, DateTimeOffset? endTime, DateTimeOffset lastModifiedOnClient,
            DateTimeOffset? originalLastModifiedOnClient, int activityType, bool isLocalOnly, int eTag,
            string packageIdHash, string platformDeviceId, string devicePlatform, string timeZone, string payload, string clipboardPayload)
        {
            Id = id;
            Executable = executable;
            DisplayText = displayText;
            ContentInfo = contentInfo;
            LastModifiedTime = lastModifiedTime;
            ExpirationTime = expirationTime;
            CreatedInCloud = createdInCloud;
            StartTime = startTime;
            EndTime = endTime;

           

            if (endTime != null && startTime!=endTime)
            {
                if (endTime.Value.Year > 1970)
                {
                    Duration = endTime.Value.Subtract(startTime);
                }
            }

            LastModifiedOnClient = lastModifiedOnClient;
            OriginalLastModifiedOnClient = originalLastModifiedOnClient;
            ActivityTypeOrg = activityType;
            ActivityType = (ActivityTypes) activityType;
            IsLocalOnly = isLocalOnly;
            ETag = eTag;
            PackageIdHash = packageIdHash;
            PlatformDeviceId = platformDeviceId;
            DevicePlatform = devicePlatform;
            TimeZone = timeZone;
            Payload = payload;
            ClipboardPayload = clipboardPayload;
        }

        public string Id { get; set; }
        public string Executable { get; set; }
        public string DisplayText { get; set; }
        public string Payload { get; set; }
        public string ClipboardPayload { get; set; }
        public string ContentInfo { get; set; }
        public DateTimeOffset LastModifiedTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }
        public DateTimeOffset? CreatedInCloud { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public TimeSpan? Duration { get; set; }

        public DateTimeOffset LastModifiedOnClient { get; set; }
        public DateTimeOffset? OriginalLastModifiedOnClient { get; set; }

        public int ActivityTypeOrg { get; set; }
        public ActivityTypes ActivityType { get; }

        public enum ActivityTypes
        {
            ToastNotification = 2,
            ExecuteOpen = 5,
            InFocus = 6,
            CloudClipboard = 10,
            CopyPaste = 16
        }

        public bool IsLocalOnly { get; set; }

        public int ETag { get; set; }

        public string PackageIdHash { get; set; }


        public string PlatformDeviceId { get; set; }

        public string DevicePlatform { get; set; }
        public string TimeZone { get; set; }

        public override string ToString()
        {
            return $"Exe: {Executable} DisplayText: {DisplayText} Start: {StartTime}";
        }
    }

   

    

}