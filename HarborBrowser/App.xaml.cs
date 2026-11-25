using System.Runtime.InteropServices;
using System.Windows;
using System;
using System.Linq;
using HarborBrowser.Services;
namespace HarborBrowser
{
    public partial class App:Application
    {
        private BrowserInstanceService _instance;
        private readonly Func<MainWindow> _windowFactory;
        private readonly string[] _startupArguments;
        private readonly string _instanceSuffix;
        public App(){}
        internal App(Func<MainWindow> factory,string[] arguments,string instanceSuffix){_windowFactory=factory;_startupArguments=arguments;_instanceSuffix=instanceSuffix;}
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        protected override void OnStartup(StartupEventArgs e)
        {
            SetCurrentProcessExplicitAppUserModelID("HarborBrowser.Desktop");
            string[] targets;
            try{targets=ExternalNavigationService.Targets(_startupArguments??e.Args).ToArray();}
            catch(Exception ex){MessageBox.Show(ex.Message,"Harbor link");Shutdown(2);return;}
            _instance=new BrowserInstanceService(_instanceSuffix??"");
            if(!_instance.IsPrimary)
            {
                if(!_instance.Forward(targets))MessageBox.Show("Harbor could not pass this link to the open window. Close any older Harbor version and try again.","Harbor link");
                Shutdown();return;
            }
            var window=_windowFactory==null?new MainWindow():_windowFactory();MainWindow=window;
            _ = _instance.Listen(values=>Dispatcher.BeginInvoke(new Action(()=>window.OpenExternalTargets(values))));
            window.OpenExternalTargets(targets);window.Show();
            base.OnStartup(e);
        }
        protected override void OnExit(ExitEventArgs e){_instance?.Dispose();base.OnExit(e);}
    }
}
