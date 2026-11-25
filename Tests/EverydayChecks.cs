using System;
using System.Linq;
using HarborBrowser.Models;
using HarborBrowser.Services;

internal static partial class Program
{
    private static void EverydayChecks()
    {
        var recent=new RecentlyClosedTabsService();
        recent.Remember(new ClosedTabSnapshot{Url="https://private.test/"},true);
        recent.Remember(new ClosedTabSnapshot{Url="javascript:alert(1)"},false);
        Check("private and executable URLs excluded from closed tabs",recent.Count==0);
        var saved=new ClosedTabSnapshot{Url="https://normal.test/",Title="Original",Index=3,IsPinned=true,GroupName="Work"};
        recent.Remember(saved,false);saved.Title="Changed";
        recent.Remember(new ClosedTabSnapshot{Url="harbor://downloads"},false);
        Equal("closed tabs restored newest first","harbor://downloads",recent.TakeLast().Url);
        var restored=recent.TakeLast();Check("closed tab snapshot preserves state",restored.Title=="Original"&&restored.Index==3&&restored.IsPinned&&restored.GroupName=="Work");
        Check("empty restore is safe",recent.TakeLast()==null);
        for(int i=0;i<25;i++)recent.Remember(new ClosedTabSnapshot{Url="https://normal.test/"+i},false);
        Check("closed tab list bounded",recent.Count==20);
        Equal("bounded stack still restores newest","https://normal.test/24",recent.TakeLast().Url);
        var bookmarks=new[]{new Bookmark{Title="Harbor docs",Url="https://docs.test/guide"},new Bookmark{Title="Unsafe",Url="javascript:alert(1)"}};
        var history=new[]{new HistoryEntry{Title="Duplicate",Url="https://docs.test/guide",VisitedAt=DateTime.Now},new HistoryEntry{Title="Harbor article",Url="https://article.test/Harbor",VisitedAt=DateTime.Now},new HistoryEntry{Title="Harbor credentials",Url="https://secret:password@private.test/"},new HistoryEntry{Title="Harbor local",Url="file:///c:/private"}};
        var matches=AddressSuggestionService.Find("harbor",bookmarks,history,false);
        Check("suggestions deduplicate bookmarks and history",matches.Count==2&&matches[0].SourceLabel=="Bookmark");
        Check("private suggestions contain only bookmarks",AddressSuggestionService.Find("harbor",bookmarks,history,true).All(s=>s.SourceLabel=="Bookmark"));
        Check("multiword suggestions match title and URL",AddressSuggestionService.Find("docs guide",bookmarks,history,false).Count==1);
        Check("empty query does not expose browsing list",AddressSuggestionService.Find(" ",bookmarks,history,false).Count==0);
        Check("suggestion count limited",AddressSuggestionService.Find("harbor",bookmarks,history,false,1).Count==1);
        var paths=new[]{new HistoryEntry{Title="Case",Url="https://case.test/A"},new HistoryEntry{Title="Case",Url="https://case.test/a"}};
        Check("case-sensitive paths remain distinct",AddressSuggestionService.Find("case",null,paths,false).Count==2);
    }
}
