using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HarborBrowser.Services
{
    public static class ExternalNavigationService
    {
        public const int MaximumTargets=16,MaximumTargetLength=8192;
        public static List<string> Targets(IEnumerable<string> arguments)
        {
            var result=new List<string>();
            var supplied=(arguments??Enumerable.Empty<string>()).ToArray();
            if(supplied.Contains("--"))
            {
                if(supplied.Length!=2||supplied[0]!="--")throw new ArgumentException("A Windows browser activation must contain one link or HTML file.");
                supplied=new[]{supplied[1]};
            }
            foreach(string raw in supplied)
            {
                string target=Target(raw);if(target==null)throw new ArgumentException("Harbor can open HTTP/HTTPS links and existing local .htm/.html files. Unsupported launch arguments were ignored.");
                if(result.Count>=MaximumTargets)throw new ArgumentException("Open up to 16 links at a time.");result.Add(target);
            }
            return result;
        }
        public static string Target(string raw)
        {
            if(string.IsNullOrWhiteSpace(raw)||raw.Length>MaximumTargetLength||raw.Any(char.IsControl))return null;
            raw=raw.Trim();Uri uri;
            if(Uri.TryCreate(raw,UriKind.Absolute,out uri)&&(uri.Scheme==Uri.UriSchemeHttp||uri.Scheme==Uri.UriSchemeHttps))return string.IsNullOrEmpty(uri.Host)?null:uri.AbsoluteUri;
            if(uri!=null&&uri.IsFile){if(uri.IsUnc)return null;raw=uri.LocalPath;}
            try
            {
                if(!Path.IsPathRooted(raw)||raw.StartsWith(@"\\",StringComparison.Ordinal))return null;
                string file=Path.GetFullPath(raw),extension=Path.GetExtension(file);
                if((!extension.Equals(".htm",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".html",StringComparison.OrdinalIgnoreCase))||!File.Exists(file))return null;
                return new Uri(file).AbsoluteUri;
            }
            catch(ArgumentException){return null;}catch(NotSupportedException){return null;}catch(PathTooLongException){return null;}
        }
        public static string DefaultAppsUri(Version windows)
        {return windows!=null&&windows.Build>=22000?"ms-settings:defaultapps?registeredAppMachine="+Uri.EscapeDataString(ProtonTrialIdentity.RegisteredName):"ms-settings:defaultapps";}
        public static void OpenDefaultApps(){Process.Start(new ProcessStartInfo(DefaultAppsUri(Environment.OSVersion.Version)){UseShellExecute=true});}
    }
}
