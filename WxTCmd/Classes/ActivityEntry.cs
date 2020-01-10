using System;

namespace WxTCmd.Classes
{
    public class ActivityEntry
    {
        public ActivityEntry(string id, string executable, string displayText, string contentInfo,
            DateTimeOffset lastModifiedTime, DateTimeOffset expirationTime, DateTimeOffset? createdInCloud,
            DateTimeOffset startTime, DateTimeOffset? endTime, DateTimeOffset lastModifiedOnClient,
            DateTimeOffset? originalLastModifiedOnClient, int activityType, bool isLocalOnly, int eTag,
            string packageIdHash, string platformDeviceId)
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

            Duration = TimeSpan.Zero;


            if (endTime != null) Duration = endTime.Value.Subtract(StartTime);

            LastModifiedOnClient = lastModifiedOnClient;
            OriginalLastModifiedOnClient = originalLastModifiedOnClient;
            ActivityType = activityType;
            IsLocalOnly = isLocalOnly;
            ETag = eTag;
            PackageIdHash = packageIdHash;
            PlatformDeviceId = platformDeviceId;
        }

        public string Id { get; set; }
        public string Executable { get; set; }
        public string DisplayText { get; set; }
        public string ContentInfo { get; set; }
        public DateTimeOffset LastModifiedTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }
        public DateTimeOffset? CreatedInCloud { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public TimeSpan Duration { get; set; }

        public DateTimeOffset LastModifiedOnClient { get; set; }
        public DateTimeOffset? OriginalLastModifiedOnClient { get; set; }

        public int ActivityType { get; set; }

        public bool IsLocalOnly { get; set; }

        public int ETag { get; set; }

        public string PackageIdHash { get; set; }


        public string PlatformDeviceId { get; set; }

        public override string ToString()
        {
            return $"Exe: {Executable} DisplayText: {DisplayText} Start: {StartTime}";
        }
    }
}