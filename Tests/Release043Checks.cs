using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using HarborBrowser.Services;

internal static partial class Program
{
    private static void Release043Checks()
    {
        string folder=Path.Combine(root,"Module043");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"a.js"),"document.body.dataset.test='yes'");
        string manifest="{\"manifest_version\":3,\"name\":\"Access test\",\"version\":\"1.0.0\",\"content_scripts\":[{\"matches\":[\"https://a.test/*\",\"https://b.test/*\"],\"js\":[\"a.js\"]}]}";
        File.WriteAllText(Path.Combine(folder,"manifest.json"),manifest);var package=SiteModuleService.Inspect(folder);
        var json=new JavaScriptSerializer();string allowed=SiteModuleService.Manifest(package,new[]{"https://a.test/*"});
        Check("module grants remove unchecked sites from native manifest",allowed.Contains("https://a.test/*")&&!allowed.Contains("https://b.test/*"));
        Check("restricted native manifest retains Home exclusion",allowed.Contains("https://harbor.invalid/*"));
        Check("site grants do not mutate original requested permissions",package.Sites.Count==2&&SiteModuleService.Manifest(package).Contains("https://b.test/*"));
        Check("denying every site removes all native injection rules",!SiteModuleService.Manifest(package,new string[0]).Contains("content_scripts"));
        bool rejected=false;try{SiteModuleService.Manifest(package,new[]{"https://unrequested.test/*"});}catch(ArgumentException){rejected=true;}Check("site grants cannot add an unrequested pattern",rejected);
        string original=SiteModuleService.Manifest(package);File.WriteAllText(Path.Combine(folder,"manifest.json"),allowed);
        Check("original request is preserved independently of active grants",SiteModuleService.Inspect(folder,original).Sites.Count==2&&SiteModuleService.Inspect(folder).Sites.Count==1);
        string profile=Path.Combine(root,"Schema8ModuleProfile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"data-version.txt"),"8");File.WriteAllText(Path.Combine(profile,"modules.json"),"{}");File.WriteAllText(Path.Combine(profile,"module-permissions.json"),"{}");
        var store=new BrowserDataStore(profile,Path.Combine(root,"Schema9Downloads"));
        Check("schema 8 upgrades to schema 9",File.ReadAllText(Path.Combine(profile,"data-version.txt"))=="9");
        Check("schema migration snapshots module access metadata",Directory.GetDirectories(store.BackupsFolder).Any(p=>File.Exists(Path.Combine(p,"module-permissions.json"))&&File.ReadAllText(Path.Combine(p,"data-version.txt"))=="8"));
    }
}
