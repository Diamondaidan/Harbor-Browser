using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    public sealed class SiteModulePackage
    {
        public string Folder,Name,Version,MinimumHarborVersion;
        public List<string> Sites=new List<string>();
        internal Dictionary<string,object> Manifest;
        public string Review {get{return Name+"  "+Version+"\n\nCan read and change pages at:\n"+string.Join("\n",Sites)+"\n\nRuns in an isolated script world in normal tabs. May store extension data. No private-tab or Harbor Home access. Only install code you trust: page access can expose sensitive information.";}}
    }
    // Deliberately restricted Manifest V3 content-script modules. WebView2 enforces
    // the generated manifest's site scope; Harbor never exposes host objects.
    public static class SiteModuleService
    {
        public static readonly string HarborVersion=AppVersion.Number;
        public static string Fingerprint(SiteModulePackage package)
        {
            using(var data=new MemoryStream())
            {
                using(var writer=new BinaryWriter(data,Encoding.UTF8,true))
                    foreach(string file in CheckTree(package.Folder).OrderBy(p=>p,StringComparer.Ordinal))
                    {
                        writer.Write(file.Substring(package.Folder.TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace('\\','/'));
                        byte[] bytes=File.ReadAllBytes(file);writer.Write(bytes.Length);writer.Write(bytes);
                    }
                data.Position=0;
                using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");
            }
        }
        private static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=30};
        private static readonly HashSet<string> FileTypes=new HashSet<string>(new[]{".json",".js",".css",".png",".jpg",".jpeg",".svg",".ico",".txt",".md",".webp"},StringComparer.OrdinalIgnoreCase);
        public static SiteModulePackage Inspect(string folder,string originalManifest=null)
        {
            folder=Path.GetFullPath(folder);CheckTree(folder);
            string path=Path.Combine(folder,"manifest.json");if(!File.Exists(path)||new FileInfo(path).Length>262144)throw new ArgumentException("Choose a module with a manifest.json smaller than 256 KB.");
            var manifest=Json.DeserializeObject(originalManifest??File.ReadAllText(path)) as Dictionary<string,object>;if(manifest==null)throw new ArgumentException("Invalid module manifest.");
            Keys(manifest,"manifest_version","name","version","description","icons","permissions","content_scripts");
            if(Convert.ToString(Value(manifest,"manifest_version"))!="3")throw new ArgumentException("Harbor supports Manifest V3 site modules.");
            string name=Required(manifest,"name",150),version=Required(manifest,"version",40);
            Version v;if(!System.Version.TryParse(version,out v)||v.Major<0)throw new ArgumentException("Use a numeric module version, such as 1.0.0.");
            foreach(var permission in Array(Value(manifest,"permissions")))if(Convert.ToString(permission)!="storage")throw new ArgumentException("This release only supports the storage permission and declared site access.");
            var icons=Value(manifest,"icons") as Dictionary<string,object>;if(icons!=null)foreach(var icon in icons.Values)LocalFile(folder,Convert.ToString(icon));
            var result=new SiteModulePackage{Folder=folder,Name=name,Version=version,Manifest=manifest};
            foreach(var raw in Array(Value(manifest,"content_scripts")))
            {
                var script=raw as Dictionary<string,object>;if(script==null)throw new ArgumentException("Invalid content script.");
                Keys(script,"matches","exclude_matches","js","css","run_at","world","all_frames");
                if(Value(script,"world")!=null&&Convert.ToString(Value(script,"world"))!="ISOLATED"||Value(script,"all_frames")!=null&&!Equals(Value(script,"all_frames"),false))throw new ArgumentException("Modules must use isolated scripts in the top frame only.");
                if(Value(script,"run_at")!=null&&!new[]{"document_start","document_end","document_idle"}.Contains(Convert.ToString(Value(script,"run_at"))))throw new ArgumentException("Invalid script timing.");
                var matches=Array(Value(script,"matches")).Select(Convert.ToString).ToList();if(matches.Count==0)throw new ArgumentException("Declare at least one HTTP or HTTPS site.");
                foreach(string pattern in matches.Concat(Array(Value(script,"exclude_matches")).Select(Convert.ToString)))
                    if(pattern==null||!Regex.IsMatch(pattern,@"^https?://(?:\*|(?:\*\.)?[a-zA-Z0-9](?:[a-zA-Z0-9.-]*[a-zA-Z0-9])?)/[^\s]*$"))throw new ArgumentException("Use HTTP or HTTPS match patterns, such as https://example.com/*.");
                result.Sites.AddRange(matches);
                var files=Array(Value(script,"js")).Select(Convert.ToString).Concat(Array(Value(script,"css")).Select(Convert.ToString)).ToList();
                if(files.Count==0)throw new ArgumentException("Declare a local script or stylesheet.");foreach(string file in files)LocalFile(folder,file);
                script["world"]="ISOLATED";script["all_frames"]=false;
                script["exclude_matches"]=Array(Value(script,"exclude_matches")).Select(Convert.ToString).Concat(new[]{"https://harbor.invalid/*","https://"+BrowserVpnService.ControlHost+"/*","https://account.proton.me/*","https://account.protonvpn.com/*","https://account-api.proton.me/*"}).Distinct().ToArray();
            }
            if(result.Sites.Count==0)throw new ArgumentException("Declare at least one content script.");result.Sites=result.Sites.Distinct().ToList();
            path=Path.Combine(folder,"harbor-module.json");
            if(File.Exists(path))
            {
                if(new FileInfo(path).Length>16384)throw new ArgumentException("Module compatibility file is too large.");
                var compatibility=Json.DeserializeObject(File.ReadAllText(path)) as Dictionary<string,object>;if(compatibility==null)throw new ArgumentException("Invalid compatibility file.");
                Keys(compatibility,"api_version","minimum_harbor_version");
                if(Convert.ToString(Value(compatibility,"api_version"))!="1")throw new ArgumentException("Unsupported Harbor module API version.");
                result.MinimumHarborVersion=Required(compatibility,"minimum_harbor_version",40);
                if(!System.Version.TryParse(result.MinimumHarborVersion,out v)||Normalized(v)>Normalized(new Version(HarborVersion)))throw new ArgumentException("This module needs a newer Harbor version.");
            }
            return result;
        }
        private static Version Normalized(Version v){return new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));}
        public static string Manifest(SiteModulePackage package,IEnumerable<string> allowedSites=null)
        {
            var manifest=Json.DeserializeObject(Json.Serialize(package.Manifest)) as Dictionary<string,object>;
            if(allowedSites!=null)
            {
                var allowed=new HashSet<string>(allowedSites,StringComparer.Ordinal);
                if(allowed.Any(s=>!package.Sites.Contains(s)))throw new ArgumentException("Choose only the sites requested by this module.");
                var scripts=new List<object>();
                foreach(var raw in Array(Value(manifest,"content_scripts")))
                {
                    var script=(Dictionary<string,object>)raw;var matches=Array(Value(script,"matches")).Select(Convert.ToString).Where(allowed.Contains).ToArray();
                    if(matches.Length==0)continue;script["matches"]=matches;scripts.Add(script);
                }
                if(scripts.Count==0)manifest.Remove("content_scripts");else manifest["content_scripts"]=scripts;
            }
            return Json.Serialize(manifest);
        }
        public static string Stage(SiteModulePackage reviewed,string root)
        {
            // Copy before reviewing again, so the engine never executes the user's
            // mutable source directory or a file that was not part of the review.
            Directory.CreateDirectory(root);RejectLink(root);string destination=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
            try
            {
                foreach(string source in CheckTree(reviewed.Folder))
                {
                    string relative=source.Substring(reviewed.Folder.TrimEnd(Path.DirectorySeparatorChar).Length+1);string target=Path.Combine(destination,relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target);
                }
                var copy=Inspect(destination);
                if(copy.Name!=reviewed.Name||copy.Version!=reviewed.Version||!copy.Sites.SequenceEqual(reviewed.Sites))throw new ArgumentException("The module changed during review. Choose it again.");
                File.WriteAllText(Path.Combine(destination,"manifest.json"),Json.Serialize(copy.Manifest),new UTF8Encoding(false));return destination;
            }
            catch{RemoveManaged(root,destination);throw;}
        }
        public static void RemoveManaged(string root,string folder)
        {
            root=Path.GetFullPath(root);folder=Path.GetFullPath(folder);Guid id;
            RejectLink(root);
            if(!string.Equals(Path.GetDirectoryName(folder),root,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(folder),"N",out id))throw new ArgumentException("Invalid managed module folder.");
            if(!Directory.Exists(folder))return;CheckTree(folder);Directory.Delete(folder,true);
        }
        private static List<string> CheckTree(string folder)
        {
            RejectLink(folder);var result=new List<string>();long size=0;
            var pending=new Stack<string>();pending.Push(folder);int directories=0;
            while(pending.Count>0)
            {
                string current=pending.Pop();if(++directories>500)throw new ArgumentException("Module has too many folders.");
                foreach(string entry in Directory.EnumerateFileSystemEntries(current))
                {
                    RejectLink(entry);if(Directory.Exists(entry)){pending.Push(entry);continue;}
                    if(!FileTypes.Contains(Path.GetExtension(entry)))throw new ArgumentException("Unsupported module file: "+Path.GetFileName(entry));
                    size+=new FileInfo(entry).Length;result.Add(entry);if(result.Count>500||size>20*1024*1024)throw new ArgumentException("Modules are limited to 500 files and 20 MB.");
                }
            }
            return result;
        }
        private static void RejectLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("Module files and folders cannot be links.");}
        private static void LocalFile(string folder,string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(":")||relative.Split('/','\\').Any(p=>p==".."||p=="."))throw new ArgumentException("Module assets must be local files within the module folder.");
            string path=Path.GetFullPath(Path.Combine(folder,relative));if(!path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new ArgumentException("Module asset is missing: "+relative);
        }
        private static object Value(Dictionary<string,object> d,string k){object v;return d.TryGetValue(k,out v)?v:null;}
        private static IEnumerable<object> Array(object value){if(value==null)return Enumerable.Empty<object>();var list=value as object[];if(list==null)throw new ArgumentException("Expected a module list.");return list;}
        private static string Required(Dictionary<string,object> d,string k,int max){var v=Value(d,k) as string;if(string.IsNullOrWhiteSpace(v)||v.Length>max||v.Any(char.IsControl))throw new ArgumentException("Invalid module "+k+".");return v;}
        private static void Keys(Dictionary<string,object> d,params string[] allowed){foreach(string key in d.Keys)if(!allowed.Contains(key))throw new ArgumentException("Unsupported module option: "+key+". This release supports site scripts, styles and storage only.");}
    }
}
