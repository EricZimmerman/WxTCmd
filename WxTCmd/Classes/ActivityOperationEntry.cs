using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WxTCmd.Classes
{
   public class ActivityOperationEntry
    {
        public ActivityOperationEntry(string id, int operationOrder, string appId, string executable, int activityType, DateTimeOffset lastModifiedTime, DateTimeOffset expirationTime, string payload, DateTimeOffset createdTime, DateTimeOffset? endTime, DateTimeOffset lastModifiedTimeOnClient, DateTimeOffset operationExpirationTime, string platformDeviceId, int operationType, string devicePlatform, string timeZone)
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

            Duration = TimeSpan.Zero;

            if (endTime != null)
            {
                if (endTime.Value.Year > 1970)
                {
                    Duration = endTime.Value.Subtract(CreatedTime);
                }
                
            }

            LastModifiedTimeOnClient = lastModifiedTimeOnClient;
            OperationExpirationTime = operationExpirationTime;
            PlatformDeviceId = platformDeviceId;
            OperationType = operationType;
            DevicePlatform = devicePlatform;
            TimeZone = timeZone;
        }

        public string Id { get; set; }

        public int OperationOrder { get; set; }
        public int OperationType { get; set; }

        public string AppId { get; set; }

        public string Executable { get; set; }

        
        public string DevicePlatform { get; set; }
        public string TimeZone { get; set; }
        


      public int ActivityTypeOrg { get; set; }
      public ActivityEntry.ActivityTypes ActivityType { get; }

      public TimeSpan Duration { get; set; }

        public DateTimeOffset LastModifiedTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }

        public string Payload { get; set; }

        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public DateTimeOffset LastModifiedTimeOnClient { get; set; }
        public DateTimeOffset OperationExpirationTime { get; set; }

        public string PlatformDeviceId { get; set; }

    }
}
