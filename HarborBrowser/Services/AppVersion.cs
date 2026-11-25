using System.Reflection;
using HarborBrowser.Updates;

namespace HarborBrowser.Services
{
    public static class AppVersion
    {
        public static string Number { get { return typeof(AppVersion).Assembly.GetName().Version.ToString(3); } }
        public static string InformationalVersion { get { return typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??Number; } }
        public static string DisplayNumber { get { return BuildIdentity.Parse(InformationalVersion).DisplayVersion; } }
        public static string Name { get { return "Harbor Browser " + DisplayNumber; } }
        public static string StatusLabel { get { return "Harbor " + DisplayNumber + "  •  WebView2"; } }
    }
}
