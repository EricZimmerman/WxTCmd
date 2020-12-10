namespace WxTCmd.Classes
{
    public class PayloadData
    {
        public string DisplayText { get; set; }
        public string ActivationUri { get; set; }
        public string AppDisplayName { get; set; }
        public string Description { get; set; }
        public string BackgroundColor { get; set; }
        public string ContentUri { get; set; }
        public ShellContentDescription2 ShellContentDescription { get; set; }


        public string Type { get; set; }
        public string ReportingApp { get; set; }
        public int ActiveDurationSeconds { get; set; }
        public string DevicePlatform { get; set; }
        public string UserTimezone { get; set; }
    }

    public class ShellContentDescription2
    {
        public string FileShellLink { get; set; }
    }


   
}