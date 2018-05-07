namespace WxTCmd.Classes
{
    public class AppIdInfo
    {
        public string Application { get; set; }
        public string Platform { get; set; }

        public override string ToString()
        {
            return $"Platform: {Platform} App: {Application}";
        }
    }
}