using System;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Services;
internal static partial class Program
{
    private static void ResolutionChecks()
    {
        var resolutions=new[]{Tuple.Create(1366,768),Tuple.Create(1280,720),Tuple.Create(1920,1080),Tuple.Create(3840,2160)};
        foreach(var size in resolutions)foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})
        {
            var work=new PixelRect(-size.Item1,0,0,size.Item2-(int)(40*scale));
            var requested=new PixelRect(-size.Item1-100,-80,-size.Item1-100+(int)(1320*scale),-80+(int)(850*scale));
            var fit=WindowFitService.FitBounds(requested,work,scale,scale);
            Check(size.Item1+"x"+size.Item2+" scale "+scale+" window fits",fit.Left>=work.Left&&fit.Top>=work.Top&&fit.Right<=work.Right&&fit.Bottom<=work.Bottom);
            Check(size.Item1+"x"+size.Item2+" scale "+scale+" positive client area",fit.Width>0&&fit.Height>0);
            var pip=WindowFitService.BottomRight(work,500,330,scale,scale);
            Check(size.Item1+"x"+size.Item2+" scale "+scale+" PiP fits",pip.Left>=work.Left&&pip.Top>=work.Top&&pip.Right<=work.Right&&pip.Bottom<=work.Bottom);
        }
        var limits=new ResourceDictionary();WindowFitService.SetMenuLimits(limits,500,360);
        Check("short-screen menu height bounded",(double)limits["HarborMenuMaxHeight"]<=336);
        Check("narrow-screen menu width bounded",(double)limits["HarborMenuMaxWidth"]<=476);
        Check("narrow toolbar hides engine",!ResponsiveLayout.ShowEngine(683));
        Check("wide toolbar keeps engine",ResponsiveLayout.ShowEngine(1100));
        Check("compact title hides wordmark",!ResponsiveLayout.ShowWordmark(683));
    }
}
