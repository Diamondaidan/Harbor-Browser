using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace HarborBrowser.Updates
{
    public sealed class BuildIdentity
    {
        public Version Version {get;private set;}
        public int Candidate {get;private set;}
        public bool IsCandidate {get{return Candidate>0;}}
        public string Number {get{return Version.ToString(3);}}
        public string Channel {get{return IsCandidate?"rc"+Candidate:"release";}}
        public string InformationalVersion {get{return Number+(IsCandidate?"-rc."+Candidate:"");}}
        public string DisplayVersion {get{return Number+(IsCandidate?" RC"+Candidate:"");}}
        private BuildIdentity(){}
        public static BuildIdentity Parse(string value)
        {
            var match=Regex.Match(value??"",@"^((?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))(?:-rc\.([1-9][0-9]*))?$");
            int candidate=0;
            if(!match.Success||(match.Groups[2].Success&&!int.TryParse(match.Groups[2].Value,out candidate)))throw new InvalidDataException("Invalid Harbor build identity.");
            return new BuildIdentity{Version=UpdateTrust.VersionOf(match.Groups[1].Value),Candidate=candidate};
        }
        public static BuildIdentity FromFile(string path)
        {
            var file=FileVersionInfo.GetVersionInfo(LocalPath(path));
            var build=Parse(file.ProductVersion?.Trim());
            if(build.Number!=file.FileMajorPart+"."+file.FileMinorPart+"."+file.FileBuildPart)throw new InvalidDataException("Harbor build and file versions do not match.");
            return build;
        }
        internal static string LocalPath(string path)
        {
            if(!Regex.IsMatch(path??"",@"^[A-Za-z]:[\\/]"))throw new InvalidDataException("A local absolute browser path is required.");
            return Path.GetFullPath(path);
        }
        public bool CanUpdateTo(string stableVersion)
        {
            var latest=UpdateTrust.VersionOf(stableVersion);
            return latest>Version||(latest==Version&&IsCandidate);
        }
    }
}
