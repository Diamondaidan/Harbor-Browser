using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;

namespace HarborBrowser.Services
{
    // Apply to app-owned WPF windows only; never change Windows theme settings.
    public static class WindowTitleBarService
    {
        private static bool _initialized;
        private static readonly ConditionalWeakTable<Window, Hook> Hooks = new ConditionalWeakTable<Window, Hook>();
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);

        public static void Initialize()
        {
            if(_initialized)return;
            _initialized=true;
            CommandManager.RegisterClassCommandBinding(typeof(Window),new CommandBinding(SystemCommands.CloseWindowCommand,(s,e)=>SystemCommands.CloseWindow((Window)(e.Parameter??s))));
            CommandManager.RegisterClassCommandBinding(typeof(Window),new CommandBinding(SystemCommands.MinimizeWindowCommand,(s,e)=>SystemCommands.MinimizeWindow((Window)(e.Parameter??s))));
            CommandManager.RegisterClassCommandBinding(typeof(Window),new CommandBinding(SystemCommands.MaximizeWindowCommand,(s,e)=>SystemCommands.MaximizeWindow((Window)(e.Parameter??s))));
            CommandManager.RegisterClassCommandBinding(typeof(Window),new CommandBinding(SystemCommands.RestoreWindowCommand,(s,e)=>SystemCommands.RestoreWindow((Window)(e.Parameter??s))));
            EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
        }
        private static void Loaded(object sender,RoutedEventArgs args)
        {
            var window=sender as Window;if(window==null||window.WindowStyle==WindowStyle.None)return;
            // An implicit Window style is not automatically inherited by every
            // derived dialog class. Give those dialogs the shared caption too.
            Prepare(window);
            window.ApplyTemplate();
            Hook hook;
            if(!Hooks.TryGetValue(window,out hook)){hook=new Hook(window);Hooks.Add(window,hook);}
            Apply(window);
        }
        internal static void Prepare(Window window)
        {
            if(window!=null&&window.WindowStyle!=WindowStyle.None&&window.Style==null)
                window.SetResourceReference(FrameworkElement.StyleProperty,typeof(Window));
        }
        public static void Apply(Window window)
        {
            if(window==null||window.WindowStyle==WindowStyle.None||!window.IsLoaded)return;
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            bool dark=window.TryFindResource("HarborDarkMode") as bool? ?? false;
            int value=dark?1:0;
            try
            {
                if(DwmSetWindowAttribute(handle,20,ref value,4)<0)DwmSetWindowAttribute(handle,19,ref value,4);
                var panel=window.TryFindResource("HarborPanel") as SolidColorBrush;
                var text=window.TryFindResource("HarborText") as SolidColorBrush;
                if(panel!=null){value=ColorRef(panel.Color);DwmSetWindowAttribute(handle,35,ref value,4);}
                if(text!=null){value=ColorRef(text.Color);DwmSetWindowAttribute(handle,36,ref value,4);}
            }
            catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        internal static bool? ReadDarkCaption(Window window)
        {
            int value;var handle=new WindowInteropHelper(window).Handle;
            if(DwmGetWindowAttribute(handle,20,out value,4)>=0)return value!=0;
            if(DwmGetWindowAttribute(handle,19,out value,4)>=0)return value!=0;
            return null;
        }
        private static int ColorRef(Color c){return c.R|(c.G<<8)|(c.B<<16);}
        private sealed class Hook
        {
            private readonly Window _window;
            private readonly HwndSource _source;
            public Hook(Window window)
            {
                _window=window;_source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                _source?.AddHook(Message);
                window.Closed+=(s,e)=>{try{_source?.RemoveHook(Message);}catch(InvalidOperationException){}};
            }
            private IntPtr Message(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam,ref bool handled)
            {
                if(message==0x031A||message==0x0320)_window.Dispatcher.BeginInvoke(new Action(()=>Apply(_window)));
                return IntPtr.Zero;
            }
        }
    }
}
