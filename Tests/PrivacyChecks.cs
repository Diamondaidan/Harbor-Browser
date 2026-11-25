using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarborBrowser.Models;
using HarborBrowser.Services;

internal static partial class Program
{
    private static int CrashProfile(string directory)
    {
        var data=new BrowserDataStore(directory,Path.Combine(directory,"Downloads"));
        var journal=new SessionRecoveryService(data);journal.Begin();
        data.SaveSession(new BrowserSession{Tabs=new List<BrowserSessionTab>{new BrowserSessionTab{Url="https://crash.test/saved",GroupName="Work",IsPinned=true}},SelectedTabIndex=0});
        Process.GetCurrentProcess().Kill();return 91;
    }
    private static void PrivacyChecks()
    {
        var now=DateTime.UtcNow;var policy=new SitePermissionService();
        Equal("permission origins strip paths and default ports","https://example.test",SitePermissionService.Origin("https://EXAMPLE.test:443/path?q=1"));
        Equal("permission origins retain non-default port","https://example.test:8443",SitePermissionService.Origin("https://example.test:8443/path"));
        Check("permission origins reject unsafe and credential URLs",new[]{"file:///C:/test","data:text/html,x","https://user:password@example.test","not a URL"}.All(s=>SitePermissionService.Origin(s)==null));
        Equal("unconfigured site asks","Ask",policy.Get("https://camera.test","https://top.test","Camera"));
        policy.Set("https://camera.test/x","https://top.test/y","Camera","Allow",now);
        Equal("permission decision applies to exact origin pair","Allow",policy.Get("https://camera.test/z","https://top.test/other","Camera"));
        Check("permission grants cannot cross embedding site port protocol or kind",policy.Get("https://camera.test","https://other.test","Camera")=="Ask"&&policy.Get("http://camera.test","https://top.test","Camera")=="Ask"&&policy.Get("https://camera.test:8443","https://top.test","Camera")=="Ask"&&policy.Get("https://camera.test","https://top.test","Microphone")=="Ask");
        Equal("unknown permissions fail closed","Block",policy.Get("https://camera.test","https://top.test","FuturePermission"));
        var copy=policy.Snapshot();copy[0].Decision="Block";Equal("permission snapshots are independent","Allow",policy.Get("https://camera.test","https://top.test","Camera"));
        policy.Set("https://camera.test","https://top.test","Camera","Ask",now);Check("Ask removes remembered choice",policy.Snapshot().Count==0);
        policy.Set("https://old.test","https://old.test","Camera","Block",now.AddDays(-2));policy.Set("https://new.test","https://new.test","Microphone","Block",now.AddMinutes(-5));
        var request=new BrowsingDataRequest{Range=BrowsingTimeRange.LastHour,History=true,Permissions=true};request.SetBounds(now);
        Check("last-hour bounds include matching timestamps",request.Contains(now.AddMinutes(-5))&&!request.Contains(now.AddDays(-2))&&!request.Contains(now.AddMinutes(2)));
        policy.Clear(request);Check("time-scoped permission cleanup retains old rules",policy.Snapshot().Count==1&&policy.Snapshot()[0].Origin=="https://old.test");
        foreach(var pair in new[]{Tuple.Create(BrowsingTimeRange.LastDay,1),Tuple.Create(BrowsingTimeRange.LastWeek,7),Tuple.Create(BrowsingTimeRange.LastFourWeeks,28)}){var r=new BrowsingDataRequest{Range=pair.Item1};r.SetBounds(now);Check("time range "+pair.Item1+" uses UTC bounds",r.StartUtc==now.AddDays(-pair.Item2)&&r.EndUtc.Kind==DateTimeKind.Utc);}
        var all=new BrowsingDataRequest{Range=BrowsingTimeRange.AllTime};all.SetBounds(now);Check("all-time cleanup includes clock-skewed records",all.Contains(now.AddDays(2))&&all.Contains(DateTime.MinValue));
        string profile=Path.Combine(root,"PrivacyProfile");var store=new BrowserDataStore(profile,Path.Combine(profile,"Downloads"));
        store.SavePermissions(policy.Snapshot());store.SavePermissions(policy.Snapshot());
        File.WriteAllText(Path.Combine(profile,"permissions.json"),"broken");Check("corrupt permission file never resurrects backup grants",store.LoadPermissions().Count==0);
        store.SavePermissions(policy.Snapshot());
        var history=new List<HistoryEntry>{new HistoryEntry{Url="https://old.test",VisitedAt=now.AddDays(-2)},new HistoryEntry{Url="https://new.test",VisitedAt=now.AddMinutes(-5)}};
        store.SaveHistory(history);store.SaveHistory(history);
        string backup=Path.Combine(store.BackupsFolder,"privacy-test"),upgrade=Path.Combine(profile,"UpgradeBackups","privacy-test");Directory.CreateDirectory(backup);Directory.CreateDirectory(upgrade);
        foreach(string folder in new[]{backup,upgrade}){File.Copy(Path.Combine(profile,"history.json"),Path.Combine(folder,"history.json"));File.WriteAllText(Path.Combine(folder,"session.json"),"{\"private\":\"old URL\"}");File.Copy(Path.Combine(profile,"permissions.json"),Path.Combine(folder,"permissions.json"));}
        store.ClearHistoryRange(request);Check("time-scoped history cleanup retains old history",store.LoadHistory().Count==1&&store.LoadHistory()[0].Url=="https://old.test");
        Check("history backup copies are sanitized",new[]{Path.Combine(profile,"history.json.bak"),Path.Combine(backup,"history.json"),Path.Combine(upgrade,"history.json")}.All(p=>!File.ReadAllText(p).Contains("https://new.test")));
        File.WriteAllText(Path.Combine(profile,"history.json"),"broken");Check("deleted history cannot return through corrupt-primary fallback",store.LoadHistory().Count==1&&store.LoadHistory()[0].Url=="https://old.test");
        store.SaveSession(new BrowserSession{Tabs=new List<BrowserSessionTab>{new BrowserSessionTab{Url="https://kept-open.test"}}});store.SaveSession(store.LoadSession());store.SaveRecovery(new SessionRecoverySnapshot{Pending=true,Session=store.LoadSession()});store.ResetRecoveryFiles();store.SanitizeRecoveryCopies();
        Check("history cleanup retains current open session",store.LoadSession().Tabs.Single().Url=="https://kept-open.test");
        Check("recovery and upgrade session copies are reset",!store.LoadRecovery().Pending&&File.ReadAllText(Path.Combine(profile,"session.json.bak"))=="{}"&&File.ReadAllText(Path.Combine(backup,"session.json"))=="{}"&&File.ReadAllText(Path.Combine(upgrade,"session.json"))=="{}");
        store.SanitizePermissionCopies(all);Check("permission cleanup sanitizes upgrade and automatic copies",new[]{Path.Combine(profile,"permissions.json.bak"),Path.Combine(backup,"permissions.json"),Path.Combine(upgrade,"permissions.json")}.All(p=>File.ReadAllText(p)=="[]"));
        string crash=Path.Combine(root,"AbruptExitProfile");var executable=Process.GetCurrentProcess().MainModule.FileName;
        using(var child=Process.Start(new ProcessStartInfo(executable,"--crash-profile \""+crash+"\""){UseShellExecute=false,CreateNoWindow=true})){Check("crash helper terminates abruptly",child.WaitForExit(15000)&&child.ExitCode!=0);}
        var crashStore=new BrowserDataStore(crash,Path.Combine(crash,"Downloads"));
        using(var run=new SessionRecoveryService(crashStore))
        {
            run.Begin();Check("abrupt termination offers saved normal tabs",run.HasPending&&run.Snapshot.Session.Tabs.Single().Url=="https://crash.test/saved");
            Check("crash snapshot retains pin group and selection",run.Snapshot.Session.Tabs[0].IsPinned&&run.Snapshot.Session.Tabs[0].GroupName=="Work"&&run.Snapshot.Session.SelectedTabIndex==0);
            bool locked=false;using(var competing=new SessionRecoveryService(crashStore)){try{competing.Begin();}catch(IOException){locked=true;}}Check("exclusive profile lock prevents competing writers",locked);
            run.Dismiss();Check("recovery dismissal persists",!crashStore.LoadRecovery().Pending);run.MarkClean();
        }
        using(var clean=new SessionRecoveryService(crashStore)){clean.Begin();Check("clean exit does not offer a crash recovery",!clean.HasPending);clean.MarkClean();}
        File.WriteAllText(Path.Combine(crash,"run-state.json"),"invalid");using(var uncertain=new SessionRecoveryService(crashStore)){uncertain.Begin();Check("corrupt run journal conservatively preserves recovery",uncertain.HasPending);uncertain.Clear();uncertain.MarkClean();}
        crashStore.SaveSession(new BrowserSession{Tabs=new List<BrowserSessionTab>{new BrowserSessionTab{Url="https://backup.test"}}});crashStore.SaveSession(crashStore.LoadSession());File.WriteAllText(Path.Combine(crash,"session.json"),"invalid");Equal("session corruption falls back to atomic backup","https://backup.test",crashStore.LoadSession().Tabs[0].Url);
    }
}
