using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace HarborBrowser.Services
{
    public static class WindowFitService
    {
        public static readonly DependencyProperty IsEnabledProperty=DependencyProperty.RegisterAttached("IsEnabled",typeof(bool),typeof(WindowFitService),new PropertyMetadata(true));
        private static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("State",typeof(object),typeof(WindowFitService));
        public static void SetIsEnabled(DependencyObject target,bool value){target.SetValue(IsEnabledProperty,value);}
        public static bool GetIsEnabled(DependencyObject target){return (bool)target.GetValue(IsEnabledProperty);}
        public static PixelRect FitBounds(PixelRect current,PixelRect work,double scaleX,double scaleY)
        {
            int mx=Math.Min((int)Math.Ceiling(12*scaleX),Math.Max(0,work.Width/8));
            int my=Math.Min((int)Math.Ceiling(12*scaleY),Math.Max(0,work.Height/8));
            int width=Math.Max(1,Math.Min(current.Width,work.Width-2*mx));
            int height=Math.Max(1,Math.Min(current.Height,work.Height-2*my));
            int left=Math.Max(work.Left+mx,Math.Min(current.Left,work.Right-mx-width));
            int top=Math.Max(work.Top+my,Math.Min(current.Top,work.Bottom-my-height));
            return new PixelRect(left,top,left+width,top+height);
        }
        public static PixelRect BottomRight(PixelRect work,double width,double height,double scaleX,double scaleY)
        {
            var desired=new PixelRect(work.Right-(int)Math.Ceiling(width*scaleX),work.Bottom-(int)Math.Ceiling(height*scaleY),work.Right,work.Bottom);
            return FitBounds(desired,work,scaleX,scaleY);
        }
        public static void Attach(Window window,Func<IntPtr,MonitorLayout> monitor=null,Func<bool> canFit=null)
        {
            WindowTitleBarService.Prepare(window);
            if(window.GetValue(StateProperty)!=null)return;
            var state=new Attachment(window,monitor??NativeWindowGeometry.ReadMonitor,canFit??(()=>true));
            window.SetValue(StateProperty,state);
        }
        private sealed class Attachment
        {
            private readonly Window _window;
            private readonly Func<IntPtr,MonitorLayout> _monitor;
            private readonly Func<bool> _canFit;
            private readonly double _minimumWidth,_minimumHeight;
            private HwndSource _source;
            private IntPtr _handle;
            private bool _queued,_closed;
            public Attachment(Window window,Func<IntPtr,MonitorLayout> monitor,Func<bool> canFit)
            {
                _window=window;_monitor=monitor;_canFit=canFit;_minimumWidth=window.MinWidth;_minimumHeight=window.MinHeight;
                window.SourceInitialized+=(s,e)=>{
                    _handle=new WindowInteropHelper(window).Handle;
                    if(_closed||_handle==IntPtr.Zero)return;
                    _source=HwndSource.FromHwnd(_handle);_source?.AddHook(Hook);Queue();
                };
                window.Loaded+=(s,e)=>Queue();
                window.Closed+=(s,e)=>{_closed=true;if(_source!=null)_source.RemoveHook(Hook);};
            }
            private IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
            {
                if(message==0x02e0||message==0x007e||message==0x001a)Queue();
                return IntPtr.Zero;
            }
            private void Queue()
            {
                if(_closed||_queued||_handle==IntPtr.Zero||!GetIsEnabled(_window))return;
                _queued=true;_window.Dispatcher.BeginInvoke(new Action(()=>{
                    _queued=false;if(_closed||!GetIsEnabled(_window))return;
                    var monitor=_monitor(_handle);var dpi=VisualTreeHelper.GetDpi(_window);
                    double logicalWidth=monitor.Work.Width/dpi.DpiScaleX;
                    double logicalHeight=monitor.Work.Height/dpi.DpiScaleY;
                    SetMenuLimits(_window.Resources,logicalWidth,logicalHeight);
                    if(_window.Owner==null&&Application.Current!=null)SetMenuLimits(Application.Current.Resources,logicalWidth,logicalHeight);
                    if(!_canFit()||_window.WindowState!=WindowState.Normal)return;
                    _window.MinWidth=Math.Min(_minimumWidth,Math.Max(0,logicalWidth-24));
                    _window.MinHeight=Math.Min(_minimumHeight,Math.Max(0,logicalHeight-24));
                    NativeWindowGeometry.Rect current;
                    if(NativeWindowGeometry.GetWindowRect(_handle,out current))
                    {
                        var bounds=FitBounds(current.Pixels,monitor.Work,dpi.DpiScaleX,dpi.DpiScaleY);
                        if(bounds.Left!=current.Left||bounds.Top!=current.Top||bounds.Width!=current.Pixels.Width||bounds.Height!=current.Pixels.Height)
                            NativeWindowGeometry.SetBounds(_handle,bounds);
                    }
                }),DispatcherPriority.Loaded);
            }
        }
        public static void SetMenuLimits(ResourceDictionary resources,double width,double height)
        {
            double menuWidth=Math.Max(120,Math.Min(360,width-24));
            resources["HarborMenuMaxWidth"]=menuWidth;
            resources["HarborMenuHeaderMaxWidth"]=Math.Max(60,menuWidth-56);
            resources["HarborMenuMaxHeight"]=Math.Max(100,Math.Min(560,height-24));
        }
    }
}
