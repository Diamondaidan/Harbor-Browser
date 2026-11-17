using System;
using System.IO;
using HarborBrowser.Services;
internal static partial class Program
{
    private static void VideoWindowChecks()
    {
        var monitor=new MonitorLayout(new PixelRect(0,0,1920,1080),new PixelRect(0,0,1920,1040),3);
        Equal("maximize excludes taskbar","1040",monitor.MaximizedRelativeBounds().Height.ToString());
        Equal("video fullscreen retains complete monitor","1080",monitor.Bounds.Height.ToString());
        Check("bottom taskbar edge recognized",monitor.AtTaskbarEdge(500,1079));
        Check("interior does not expose taskbar",!monitor.AtTaskbarEdge(500,600));
        Check("other monitor cursor ignored",!monitor.AtTaskbarEdge(2200,1079));
        var negative=new MonitorLayout(new PixelRect(-1920,-500,0,580),new PixelRect(-1920,-460,0,580),1);
        Equal("negative monitor max X","0",negative.MaximizedRelativeBounds().Left.ToString());
        Equal("top taskbar max offset","40",negative.MaximizedRelativeBounds().Top.ToString());
        Check("negative top edge recognized",negative.AtTaskbarEdge(-500,-500));
        var left=new MonitorLayout(new PixelRect(-1920,0,0,1080),new PixelRect(-1880,0,0,1080),0);
        Equal("left taskbar leaves usable width","1880",left.TaskbarSafeArea().Width.ToString());
        Check("left taskbar edge recognized",left.AtTaskbarEdge(-1920,500));
        var right=new MonitorLayout(new PixelRect(0,0,2560,1440),new PixelRect(0,0,2500,1440),2);
        Check("right taskbar edge recognized",right.AtTaskbarEdge(2559,500));
        Equal("right taskbar work area used","2500",right.TaskbarSafeArea().Width.ToString());
        for(int edge=0;edge<4;edge++)
        {
            var auto=new MonitorLayout(new PixelRect(0,0,1920,1080),new PixelRect(0,0,1920,1080),edge){TaskbarBand=96};
            var safe=auto.TaskbarSafeArea();
            Check("auto-hide taskbar space at edge "+edge,edge%2==0?safe.Width==1824:safe.Height==984);
        }
        Equal("ready video score parsed","12345",VideoScriptService.Score("{\"found\":true,\"score\":12345}").ToString());
        Equal("no-video probe ignored","0",VideoScriptService.Score("{\"found\":false,\"score\":12345}").ToString());
        Equal("bad video probe ignored","0",VideoScriptService.Score("malformed").ToString());
        Check("video script embedded",VideoScriptService.Create("testchannel").Contains("testchannel"));
        bool rejected=false;try{VideoScriptService.Command("delete");}catch(ArgumentException){rejected=true;}
        Check("video command allowlist",rejected);
        var store=new BrowserDataStore(Path.Combine(root,"isolated-profile"),Path.Combine(root,"isolated-downloads"));
        var settings=new HarborBrowser.Models.BrowserSettings{ThemeMode="Dark"};
        store.SaveSettings(settings);
        Equal("isolated profile settings save/load","Dark",store.LoadSettings().ThemeMode);
        Check("isolated profile path honored",store.RootFolder.StartsWith(root,StringComparison.OrdinalIgnoreCase));
    }
}
