using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WxTCmd.Classes
{
   public class ActivityOperationEntry
    {
        public ActivityOperationEntry(int operationOrder, string appId, string executable, int activityType, DateTimeOffset lastModifiedTime, DateTimeOffset expirationTime, string payload, DateTimeOffset createdTime, DateTimeOffset? endTime, DateTimeOffset lastModifiedTimeOnClient, DateTimeOffset operationExpirationTime, string platformDeviceId)
        {
            OperationOrder = operationOrder;
            AppId = appId;
            Executable = executable;
            ActivityType = activityType;
            LastModifiedTime = lastModifiedTime;
            ExpirationTime = expirationTime;
            Payload = payload;
            CreatedTime = createdTime;
            EndTime = endTime;

            // if (EndTime?.Year == 1)
            // {
            //     EndTime = null;
            // }


            LastModifiedTimeOnClient = lastModifiedTimeOnClient;
            OperationExpirationTime = operationExpirationTime;
            PlatformDeviceId = platformDeviceId;
        }

        public int OperationOrder { get; set; }
        public string AppId { get; set; }

        public string Executable { get; set; }
  //      public string DisplayText { get; set; }


        public int ActivityType { get; set; }
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
