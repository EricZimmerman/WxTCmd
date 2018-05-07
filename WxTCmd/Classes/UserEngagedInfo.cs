namespace WxTCmd.Classes
{
    public class UserEngagedInfo
    {
        public string Type { get; set; }
        public string ReportingApp { get; set; }
        public long ActiveDurationSeconds { get; set; }
        public ShellContentDescription ShellContentDescription { get; set; }
        public string UserTimezone { get; set; }
    }

    public class ShellContentDescription
    {
        public long MergedGap { get; set; }
    }
}