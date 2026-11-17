using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarborBrowser.Models;
using HarborBrowser.Services;

internal static partial class Program
{
    private static void Release042Checks()
    {
        Check("few normal tabs have standard width",TabStripLayout.Width(1000,3,0)==190);
        Check("overflowing tabs shrink equally",TabStripLayout.Width(700,5,0)<190&&TabStripLayout.Width(700,6,0)<TabStripLayout.Width(700,5,0));
        Check("pinned tabs reserve space",TabStripLayout.Width(700,5,2)<TabStripLayout.Width(700,5,0));
        Check("crowded tabs retain a usable icon width",TabStripLayout.Width(500,100,0)==54);
        var bookmarks=new List<Bookmark>{new Bookmark{Title="A",Url="https://a.test/"},new Bookmark{Title="B",Url="https://b.test/"}};
        Check("legacy bookmarks receive stable IDs",BookmarkBarService.EnsureIds(bookmarks)&&!BookmarkBarService.EnsureIds(bookmarks));
        string first=bookmarks[0].Id;bookmarks.Add(bookmarks[0].Copy());BookmarkBarService.EnsureIds(bookmarks);
        Check("duplicate imported IDs are repaired",bookmarks.Select(b=>b.Id).Distinct().Count()==3&&bookmarks[0].Id==first);bookmarks.RemoveAt(2);
        Check("bookmark editing copies retain identity",bookmarks[0].Copy().Id==first&&BookmarkLibraryService.Merge(bookmarks,null)[0].Id==first);
        var settings=new BrowserSettings{BookmarkFolders=new List<string>{"Work"}};var keys=BookmarkBarService.Entries(bookmarks,settings).Select(e=>e.Key).ToList();
        settings.BookmarkBarOrder=BookmarkBarService.Move(keys,keys[2],keys[0],false);
        Check("folders and bookmarks can be interleaved",BookmarkBarService.Entries(bookmarks,settings)[0].Folder=="Work");
        settings.BookmarkBarOrder.Add("missing");settings.BookmarkBarOrder.Add(keys[0]);
        Check("stale and duplicate order entries are ignored",BookmarkBarService.Entries(bookmarks,settings).Count==3);
        var store=new BrowserDataStore(Path.Combine(root,"BarProfile"),Path.Combine(root,"BarDownloads"));store.SaveBookmarks(bookmarks);store.SaveSettings(settings);
        Check("bookmark order persists with identity",BookmarkBarService.Entries(store.LoadBookmarks(),store.LoadSettings())[0].Folder=="Work"&&store.LoadBookmarks()[0].Id==first);
        var copy=settings.Copy();copy.BookmarkBarOrder.Clear();Check("appearance drafts preserve separate bookmark order",settings.BookmarkBarOrder.Count>0);
        string module=Path.Combine(root,"SiteModule");Directory.CreateDirectory(module);File.WriteAllText(Path.Combine(module,"test.js"),"document.body.dataset.test='ok';");
        string good="{\"manifest_version\":3,\"name\":\"Test\",\"version\":\"1.0.0\",\"permissions\":[\"storage\"],\"content_scripts\":[{\"matches\":[\"https://example.com/*\"],\"js\":[\"test.js\"]}]}";
        File.WriteAllText(Path.Combine(module,"manifest.json"),good);var package=SiteModuleService.Inspect(module);
        Check("supported site module exposes reviewed site access",package.Sites.Single()=="https://example.com/*"&&package.Review.Contains("sensitive"));
        string managed=Path.Combine(root,"ManagedModules"),staged=SiteModuleService.Stage(package,managed);
        Check("module engine copy excludes Harbor Home",File.ReadAllText(Path.Combine(staged,"manifest.json")).Contains("https://harbor.invalid/*"));
        Check("staging preserves source manifest",File.ReadAllText(Path.Combine(module,"manifest.json"))==good);
        Action<string,string> reject=(name,manifest)=>{File.WriteAllText(Path.Combine(module,"manifest.json"),manifest);bool refused=false;try{SiteModuleService.Inspect(module);}catch{refused=true;}Check(name,refused);};
        reject("module background workers rejected",good.Replace("\"permissions\"","\"background\":{\"service_worker\":\"test.js\"},\"permissions\""));
        reject("native messaging permission rejected",good.Replace("\"storage\"","\"nativeMessaging\""));
        reject("all URL file access rejected",good.Replace("https://example.com/*","<all_urls>"));
        reject("main-world injection rejected",good.Replace("\"js\":","\"world\":\"MAIN\",\"js\":"));
        reject("cross-frame injection rejected",good.Replace("\"js\":","\"all_frames\":true,\"js\":"));
        reject("module traversal assets rejected",good.Replace("test.js","../test.js"));
        File.WriteAllText(Path.Combine(module,"manifest.json"),good);File.WriteAllText(Path.Combine(module,"harbor-module.json"),"{\"api_version\":1,\"minimum_harbor_version\":\"9.0.0\"}");
        bool versionBlocked=false;try{SiteModuleService.Inspect(module);}catch{versionBlocked=true;}Check("newer minimum Harbor version is blocked",versionBlocked);
        SiteModuleService.RemoveManaged(managed,staged);Check("module removal deletes only managed copy",!Directory.Exists(staged)&&Directory.Exists(module));
        bool protectedRoot=false;try{SiteModuleService.RemoveManaged(managed,module);}catch{protectedRoot=true;}Check("module cleanup refuses external folders",protectedRoot&&Directory.Exists(module));
    }
}
