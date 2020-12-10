using System;
using ServiceStack.DataAnnotations;

namespace WxTCmd.Classes
{
    [Alias("ActivityOperation")]
    public class ActivityOperation
    {
        [PrimaryKey] public int OperationOrder { get; set; }

        public Guid Id { get; set; }

        public int OperationType { get; set; }

        public string AppId { get; set; }
        public string PackageHashId { get; set; }
        public string AppActivityId { get; set; }

        public int ActivityType { get; set; }

        public Guid ParentActivityId { get; set; }

        public string Tag { get; set; }
        public string Group { get; set; }
        public string MatchId { get; set; }

        public DateTimeOffset LastModifiedTime { get; set; }

        public DateTimeOffset ExpirationTime { get; set; }

        public byte[] Payload { get; set; }

        public int Priority { get; set; }

        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset OperationExpirationTime { get; set; }

        public string PlatformDeviceId { get; set; }

        public string DdsDeviceId { get; set; }
        
        
        public DateTimeOffset CreatedInCloud { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public DateTimeOffset LastModifiedOnClient { get; set; }
        
        public string CorrelationVector { get; set; }

        public string GroupAppActivityId { get; set; }

        public string ClipboardPayload { get; set; }

        public string EnterpriseId { get; set; }

        public int UserActionState { get; set; }
        public int IsRead { get; set; }
        
        public string OriginalPayload { get; set; }
        
        public DateTimeOffset OriginalLastModifiedOnClient { get; set; }

        public int UploadAllowedByPolicy { get; set; }
        
        public string PatchFields { get; set; }
        
        public string GroupItems { get; set; }

        public DateTimeOffset ThrottleReleaseTime { get; set; }

        public int ETag { get; set; }

        public override string ToString()
        {
            return $"Operation Order: {OperationOrder} AppId: {AppId} Expire date: {ExpirationTime}";
        }
    }

  

}