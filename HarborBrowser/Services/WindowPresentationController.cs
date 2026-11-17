using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;

namespace HarborBrowser.Services
{
    public struct PixelRect
    {
        public int Left,Top,Right,Bottom;
        public PixelRect(int left,int top,int right,int bottom){Left=left;Top=top;Right=right;Bottom=bottom;}
        public int Width {get{return Right-Left;}} public int Height {get{return Bottom-Top;}}
        public bool Contains(int x,int y){return x>=Left&&x<Right&&y>=Top&&y<Bottom;}
    }
    public sealed class MonitorLayout
    {
        public PixelRect Bounds,Work;
        public int TaskbarEdge=3;
        public int TaskbarBand=48;
        public MonitorLayout(PixelRect bounds,PixelRect work,int edge=3){Bounds=bounds;Work=work;TaskbarEdge=edge;}
        public PixelRect TaskbarSafeArea()
        {
            if(Work.Width<Bounds.Width-3||Work.Height<Bounds.Height-3)return Work;
            var r=Bounds;int band=TaskbarBand;
            if(TaskbarEdge==0)r.Left+=band;else if(TaskbarEdge==1)r.Top+=band;else if(TaskbarEdge==2)r.Right-=band;else r.Bottom-=band;
            return r;
        }
        public bool AtTaskbarEdge(int x,int y)
        {
            if(!Bounds.Contains(x,y))return false;
            return TaskbarEdge==0?x<=Bounds.Left+2:TaskbarEdge==1?y<=Bounds.Top+2:
                TaskbarEdge==2?x>=Bounds.Right-3:y>=Bounds.Bottom-3;
        }
        public PixelRect MaximizedRelativeBounds()
        {
            return new PixelRect(Work.Left-Bounds.Left,Work.Top-Bounds.Top,
                Work.Right-Bounds.Left,Work.Bottom-Bounds.Top);
        }
    }
    public static class NativeWindowGeometry
    {
        [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
        [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;public PixelRect Pixels{get{return new PixelRect(Left,Top,Right,Bottom);}}}
        [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
        [StructLayout(LayoutKind.Sequential)]public struct MinMax {public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
        [StructLayout(LayoutKind.Sequential)]public struct Placement {public int Length;public uint Flags,Show;public Point Min,Max;public Rect Normal;}
        [StructLayout(LayoutKind.Sequential)]private struct AppBar {public int Size;public IntPtr Window;public uint Callback,Edge;public Rect Rect;public IntPtr Param;}
        [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Auto)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
        [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr window,out Rect rect);
        [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")]private static extern bool GetWindowPlacement(IntPtr window,ref Placement placement);
        [DllImport("user32.dll")]private static extern bool SetWindowPlacement(IntPtr window,ref Placement placement);
        [DllImport("shell32.dll")]private static extern UIntPtr SHAppBarMessage(uint message,ref AppBar data);
        public static MonitorLayout ReadMonitor(IntPtr window)
        {
            var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
            if(!GetMonitorInfo(MonitorFromWindow(window,2),ref info))throw new InvalidOperationException("Unable to read monitor bounds.");
            var b=info.Monitor.Pixels;var w=info.Work.Pixels;int edge=3;
            int gap=0;int[] gaps={w.Left-b.Left,w.Top-b.Top,b.Right-w.Right,b.Bottom-w.Bottom};
            for(int i=0;i<4;i++)if(gaps[i]>gap){gap=gaps[i];edge=i;}
            if(gap<=3)for(uint i=0;i<4;i++)
            {
                var bar=new AppBar{Size=Marshal.SizeOf(typeof(AppBar)),Edge=i,Rect=info.Monitor};
                if(SHAppBarMessage(0x0000000b,ref bar)!=UIntPtr.Zero){edge=(int)i;break;}
            }
            return new MonitorLayout(b,w,edge){TaskbarBand=Math.Max(40,(int)(GetDpiForWindow(window)*40/96))};
        }
        public static void SetBounds(IntPtr window,PixelRect bounds)
        {
            if(!SetWindowPos(window,IntPtr.Zero,bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x0014|0x0020))
                throw new InvalidOperationException("Unable to resize the window.");
        }
        public static Placement SavePlacement(IntPtr window)
        {
            var p=new Placement{Length=Marshal.SizeOf(typeof(Placement))};
            if(!GetWindowPlacement(window,ref p))throw new InvalidOperationException("Unable to save window placement.");
            return p;
        }
        public static void RestorePlacement(IntPtr window,Placement p){p.Length=Marshal.SizeOf(typeof(Placement));SetWindowPlacement(window,ref p);}
    }

    public sealed class WindowPresentationController:IDisposable
    {
        private readonly Window _window;
        private readonly Action<bool> _setChrome;
        private readonly Func<IntPtr,MonitorLayout> _monitor;
        private readonly HwndSource _source;
        private readonly DispatcherTimer _edgeTimer;
        private readonly IntPtr _handle;
        private NativeWindowGeometry.Placement _savedPlacement;
        private WindowChrome _savedChrome;
        private ResizeMode _savedResize;
        private bool _saved,_peek;
        private double _savedMinWidth,_savedMinHeight;
        public bool BrowserFullScreen {get;private set;}
        public bool VideoFullScreen {get;private set;}
        public bool IsFullScreen {get{return BrowserFullScreen||VideoFullScreen;}}
        public WindowPresentationController(Window window,Action<bool> setChrome,Func<IntPtr,MonitorLayout> monitor=null)
        {
            _window=window;_setChrome=setChrome;_monitor=monitor??NativeWindowGeometry.ReadMonitor;
            _handle=new WindowInteropHelper(window).Handle;_source=HwndSource.FromHwnd(_handle);_source.AddHook(Hook);
            _edgeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
            _edgeTimer.Tick+=(s,e)=>CheckTaskbarEdge();
        }
        public MonitorLayout Monitor {get{return _monitor(_handle);}}
        public void SetBrowserFullScreen(bool enabled){if(BrowserFullScreen==enabled)return;BrowserFullScreen=enabled;Apply();}
        public void SetVideoFullScreen(bool enabled){if(VideoFullScreen==enabled)return;VideoFullScreen=enabled;Apply();}
        private IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
        {
            if(message==0x0024&&!IsFullScreen)
            {
                var data=(NativeWindowGeometry.MinMax)Marshal.PtrToStructure(l,typeof(NativeWindowGeometry.MinMax));
                var bounds=Monitor.MaximizedRelativeBounds();
                data.MaxPosition.X=bounds.Left;data.MaxPosition.Y=bounds.Top;
                data.MaxSize.X=bounds.Width;data.MaxSize.Y=bounds.Height;
                Marshal.StructureToPtr(data,l,false);handled=true;
            }
            if((message==0x007e||message==0x02e0||message==0x001a)&&IsFullScreen)
                _window.Dispatcher.BeginInvoke(new Action(Apply),DispatcherPriority.Loaded);
            return IntPtr.Zero;
        }
        private void Apply()
        {
            if(IsFullScreen)
            {
                if(!_saved)
                {
                    _savedPlacement=NativeWindowGeometry.SavePlacement(_handle);
                    _savedChrome=WindowChrome.GetWindowChrome(_window);_savedResize=_window.ResizeMode;
                    _savedMinWidth=_window.MinWidth;_savedMinHeight=_window.MinHeight;_saved=true;
                }
                _setChrome(true);_window.MinWidth=0;_window.MinHeight=0;_window.WindowState=WindowState.Normal;_window.ResizeMode=ResizeMode.NoResize;
                WindowChrome.SetWindowChrome(_window,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(0),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
                _peek=false;NativeWindowGeometry.SetBounds(_handle,VideoFullScreen?Monitor.Bounds:Monitor.Work);
                _edgeTimer.Start();
            }
            else if(_saved)
            {
                _edgeTimer.Stop();_peek=false;_window.ResizeMode=_savedResize;_window.MinWidth=_savedMinWidth;_window.MinHeight=_savedMinHeight;
                WindowChrome.SetWindowChrome(_window,_savedChrome);_setChrome(false);
                var saved=_savedPlacement;_saved=false;NativeWindowGeometry.RestorePlacement(_handle,saved);
            }
        }
        private void CheckTaskbarEdge()
        {
            if(!IsFullScreen||!_window.IsActive||_window.WindowState==WindowState.Minimized)return;
            NativeWindowGeometry.Point cursor;if(!NativeWindowGeometry.GetCursorPos(out cursor))return;
            var monitor=Monitor;var safe=monitor.TaskbarSafeArea();
            if(!_peek&&monitor.AtTaskbarEdge(cursor.X,cursor.Y)){_peek=true;NativeWindowGeometry.SetBounds(_handle,safe);}
            else if(_peek&&safe.Contains(cursor.X,cursor.Y)&&cursor.X>safe.Left+12&&cursor.X<safe.Right-12&&cursor.Y>safe.Top+12&&cursor.Y<safe.Bottom-12)
            {_peek=false;NativeWindowGeometry.SetBounds(_handle,VideoFullScreen?monitor.Bounds:monitor.Work);}
        }
        public void Dispose(){_edgeTimer.Stop();_source.RemoveHook(Hook);}
    }
}
