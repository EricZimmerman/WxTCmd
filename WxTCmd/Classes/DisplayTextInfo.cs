namespace WxTCmd.Classes
{
    public class DisplayTextInfo
    {
        public string DisplayText { get; set; }
        public string ActivationUri { get; set; }
        public string AppDisplayName { get; set; }
        public string Description { get; set; }
        public string BackgroundColor { get; set; }
        public string ContentUri { get; set; }
        public ShellContentDescription2 ShellContentDescription { get; set; }
    }

    public class ShellContentDescription2
    {
        public string FileShellLink { get; set; }
    }
}