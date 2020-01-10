using System;

namespace WxTCmd.Classes
{
    public class ActivityPackageEntry
    {
        public ActivityPackageEntry(string id, string platform, string name, string additionalInformation,
            DateTimeOffset expires)
        {
            Id = id;
            switch (platform)
            {
                case "windows_win32":
                    Platform = "Win32";
                    break;
                case "x_exe_path":
                    Platform = "ExecutablePath";
                    break;
                case "packageId":
                    Platform = "Package";
                    break;
                default:
                    Platform = platform;
                    break;
            }

            Name = name;
            AdditionalInformation = additionalInformation;
            Expires = expires;
        }

        public string Id { get; set; }
        public string Platform { get; set; }
        public string Name { get; set; }
        public string AdditionalInformation { get; set; }

        public DateTimeOffset Expires { get; set; }

        public override string ToString()
        {
            var addlInfo = string.Empty;

            if (AdditionalInformation.Length > 0) addlInfo = $" Additional info: {AdditionalInformation}";

            return $"Platform: {Platform} Name: {Name} Expires: {Expires}{addlInfo}";
        }
    }
}