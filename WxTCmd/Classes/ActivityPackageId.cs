using System;
using ServiceStack.DataAnnotations;

namespace WxTCmd.Classes
{
    [Alias("Activity_PackageId")]
    public class ActivityPackageId
    {
        [PrimaryKey] public Guid ActivityId { get; set; }

        public string Platform { get; set; }
        public string PackageName { get; set; }

        public DateTimeOffset ExpirationTime { get; set; }

        public override string ToString()
        {
            return $"Platform: {Platform} PackageName: {PackageName} Expire date: {ExpirationTime}";
        }
    }
}