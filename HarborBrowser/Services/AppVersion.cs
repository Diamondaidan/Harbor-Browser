namespace HarborBrowser.Services
{
    public static class AppVersion
    {
        public static string Number { get { return typeof(AppVersion).Assembly.GetName().Version.ToString(3); } }
        public static string Name { get { return "Harbor Browser " + Number; } }
        public static string StatusLabel { get { return "Harbor " + Number + "  •  WebView2"; } }
    }
}
