using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarborBrowser.Models;
using HarborBrowser.Services;

internal static partial class Program
{
    private static void CustomizationChecks()
    {
        string html="<!DOCTYPE NETSCAPE-Bookmark-file-1><DL><p><DT><H3>Work &amp; notes</H3><DL><p><DT><A HREF='https://example.com/a?x=1&amp;y=2'>A &amp; B</A><DT><H3>Projects</H3><DL><p><DT><A HREF=\"https://example.com/B\">Project</A></DL><p></DL><p><DT><A HREF='javascript:123'>Unsafe</A><DT><A HREF='https://root.test/'>Root</A><DT><H3>Empty</H3><DL><p></DL><p></DL>";
        var imported=BookmarkLibraryService.Import(html,false);
        Check("HTML bookmarks parse nested folders",imported.Bookmarks.Count==3&&imported.Bookmarks[1].Folder=="Work & notes/Projects");
        Check("HTML bookmark entities decoded",imported.Bookmarks[0].Title=="A & B"&&imported.Bookmarks[0].Url.Contains("x=1&y=2"));
        Check("HTML empty folders retained",imported.Folders.Contains("Empty"));
        string exported=BookmarkLibraryService.Export(imported.Bookmarks,imported.Folders,false);
        Check("bookmark export escapes HTML",exported.Contains("A &amp; B")&&exported.Contains("x=1&amp;y=2"));
        var again=BookmarkLibraryService.Import(exported,false);
        Check("HTML bookmark roundtrip preserves hierarchy",again.Bookmarks.Select(b=>b.Folder+"|"+b.Url).OrderBy(v=>v).SequenceEqual(imported.Bookmarks.Select(b=>b.Folder+"|"+b.Url).OrderBy(v=>v))&&again.Folders.Contains("Empty"));
        var json=BookmarkLibraryService.Export(imported.Bookmarks,imported.Folders,true);
        Check("JSON bookmark roundtrip preserves empty folders",BookmarkLibraryService.Import(json,true).Folders.Contains("Empty"));
        Check("legacy JSON bookmark arrays accepted",BookmarkLibraryService.Import("[{\"Title\":\"Legacy\",\"Url\":\"https://legacy.test\"}]",true).Bookmarks.Count==1);
        Check("bookmark merges skip duplicates",BookmarkLibraryService.Merge(imported.Bookmarks,imported.Bookmarks).Count==3);
        Bookmark bookmark;Check("bookmark credentials and scripts rejected",!BookmarkLibraryService.TryBookmark("x","https://user:pass@example.com",null,out bookmark)&&!BookmarkLibraryService.TryBookmark("x","javascript:123",null,out bookmark));
        bool rejected=false;try{BookmarkLibraryService.Folder("Work/../Secrets");}catch(ArgumentException){rejected=true;}Check("invalid folder paths rejected",rejected);
        var settings=new BrowserSettings{UseCustomHomeShortcuts=true,HomeScene="Morning"};settings.HomeShortcuts.Add(new Bookmark{Title="Custom",Url="https://custom.test/"});settings.BookmarkFolders.Add("Work");settings.AppearancePresets.Add(new AppearancePreset{Name="Warm",ThemeMode="Dark",LightColors=new AppearanceColors(),DarkColors=new AppearanceColors{Accent="#123456"}});
        var copy=settings.Copy();copy.HomeShortcuts[0].Title="Changed";copy.AppearancePresets[0].DarkColors.Accent="#FFFFFF";copy.BookmarkFolders.Add("More");
        Check("customization drafts deep-copy all collections",settings.HomeShortcuts[0].Title=="Custom"&&settings.AppearancePresets[0].DarkColors.Accent=="#123456"&&settings.BookmarkFolders.Count==1);
        var state=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(HomePageService.CreateState(null,new[]{new Bookmark{Title="Automatic",Url="https://automatic.test/"}},false,settings));
        Check("custom home shortcuts override bookmark fallback",((Dictionary<string,object>)((object[])state["bookmarks"])[0])["title"].ToString()=="Custom");
        Check("home background scene conveyed",Convert.ToString(state["scene"])=="Morning");
        string profile=Path.Combine(root,"Schema6Profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"data-version.txt"),"5");File.WriteAllText(Path.Combine(profile,"settings.json"),"{\"ThemeMode\":\"Dark\"}");File.WriteAllText(Path.Combine(profile,"bookmarks.json"),"[{\"Title\":\"Existing\",\"Url\":\"https://existing.test/\"}]");
        var store=new BrowserDataStore(profile,Path.Combine(profile,"Downloads"));
        Check("schema 5 profile upgrades to schema 9",File.ReadAllText(Path.Combine(profile,"data-version.txt")).Trim()=="9");
        Check("schema upgrade snapshots prior settings",Directory.GetDirectories(store.BackupsFolder).Any(p=>File.Exists(Path.Combine(p,"settings.json"))));
        Check("schema upgrade preserves legacy bookmarks",store.LoadBookmarks().Single().Title=="Existing");
        store.SaveSettings(settings);var loaded=store.LoadSettings();Check("new home folders and presets persist",loaded.HomeShortcuts[0].Title=="Custom"&&loaded.AppearancePresets[0].Name=="Warm"&&loaded.BookmarkFolders[0]=="Work");
        string picture=Path.Combine(root,"source-picture.png");
        var pixels=new byte[]{40,80,120,255,80,120,160,255,120,160,200,255,160,200,240,255};
        var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,pixels,8);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(picture))encoder.Save(stream);
        string hash=Hash(picture),name=BackgroundImageService.Import(profile,picture),data=BackgroundImageService.ReadDataUri(profile,name);
        Check("background import copies valid image into profile",data!=null&&data.StartsWith("data:image/png;base64,")&&File.Exists(Path.Combine(profile,"HomeBackgrounds",name)));
        Check("background import preserves original image",Hash(picture)==hash);
        Check("background lookup cannot escape managed directory",BackgroundImageService.ReadDataUri(profile,"../settings.json")==null);
        bool corrupt=false;string broken=Path.Combine(root,"bad-picture.png");File.WriteAllText(broken,"not an image");try{BackgroundImageService.Import(profile,broken);}catch{corrupt=true;}Check("invalid background file rejected",corrupt);
        OnlinePolicyChecks().GetAwaiter().GetResult();
    }
    private static async Task OnlinePolicyChecks()
    {
        int requests=0;Uri last=null;
        var service=new OnlineSuggestionService((uri,token)=>{requests++;last=uri;return Task.FromResult("[\"weather\",[\"weather tomorrow\",\"weather tomorrow\",\"weather forecast\"]]");});
        await service.GetAsync("weather","google",false,false,CancellationToken.None);await service.GetAsync("weather","google",true,true,CancellationToken.None);
        foreach(string value in new[]{"https://secret.test/path","localhost","192.168.1.1","user:password","private.test/path",new string('x',161)})await service.GetAsync(value,"google",true,false,CancellationToken.None);
        Check("online toggle private mode and address guards prevent requests",requests==0);
        var terms=await service.GetAsync("weather tomorrow","duckduckgo",true,false,CancellationToken.None);
        Check("online completions deduplicate and use selected provider",terms.Count==2&&last.Host=="duckduckgo.com"&&last.Query.Contains("weather%20tomorrow"));
        Check("rich suggestion arrays parsed safely",OnlineSuggestionService.Parse("[\"x\",[[\"term\",0],[\"other\",1]]]").Count==2);
        var malformed=new OnlineSuggestionService((u,t)=>Task.FromResult("<html>unavailable</html>"));Check("provider errors preserve local suggestions",(await malformed.GetAsync("weather","bing",true,false,CancellationToken.None)).Count==0);
        var cancelled=new OnlineSuggestionService((u,t)=>Task.FromCanceled<string>(new CancellationToken(true)));Check("cancelled online lookup returns safely",(await cancelled.GetAsync("weather","brave",true,false,CancellationToken.None)).Count==0);
        Check("oversized online response rejected",OnlineSuggestionService.Parse(new string('x',65537)).Count==0);
    }
    private static int ProbeProviders()
    {
        var service=new OnlineSuggestionService();int unavailable=0;
        foreach(string engine in new[]{"duckduckgo","google","bing","brave","startpage"})
        {var values=service.GetAsync("weather",engine,true,false,CancellationToken.None).GetAwaiter().GetResult();Console.WriteLine("PROVIDER "+engine+" suggestions="+values.Count);if(values.Count==0)unavailable++;}
        return unavailable==0?0:1;
    }
}
