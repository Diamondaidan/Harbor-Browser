using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using HarborBrowser.Updates;
using HarborBrowser.Views;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private CancellationTokenSource _updateTraffic;
        internal bool UpdateNetworkAllowed {get{return !(ProtonProtection?.Enabled??false)&&!(_protonPreview?.Enabled??false)&&!(_browserVpn?.Enabled??false)&&_protonChanges==0&&_torChanges==0;}}
        private void CancelUpdateTraffic(){_updateTraffic?.Cancel();}
        private async void Updates_Click(object sender,RoutedEventArgs e)
        {
            if(_updateTraffic!=null)return;
            using(var traffic=new CancellationTokenSource())
            {
                _updateTraffic=traffic;
                try
                {
                    var service=new UpdateService(_store.RootFolder,()=>UpdateNetworkAllowed);
                    var window=new UpdatesWindow(service,AppVersion.Number,traffic.Token,_store.IsReadOnlyForNewerDataVersion,AppVersion.InformationalVersion){Owner=this};
                    if(window.ShowDialog()!=true||window.InstallChoice==null)return;
                    if(_downloads.Any(d=>d.IsActive))throw new InvalidOperationException("Finish or cancel active downloads before closing Harbor for setup.");
                    var choice=window.InstallChoice;
                    SetStatus("Verifying installer and preparing a profile backup…");
                    await PrepareUpdateLauncher(service,choice);
                    // The helper waits for this process to exit and acquires the
                    // profile lock before backing up data and opening setup.
                    Close();
                }
                catch(Exception ex){MessageBox.Show(this,ex.Message,"Harbor update");}
                finally{_updateTraffic=null;}
            }
        }
        private async Task PrepareUpdateLauncher(UpdateService service,UpdateInstallChoice choice)
        {
            string folder=Path.Combine(service.Root,"Launch",Guid.NewGuid().ToString("N"));UpdateState.SafeDirectory(folder);
            string app=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HarborBrowser.exe");
            string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HarborUpdateLauncher.exe");
            string helper=Path.Combine(folder,"HarborUpdateLauncher.exe");
            File.Copy(source,helper,false);
            if(UpdateTrust.HashFile(source)!=UpdateTrust.HashFile(helper))throw new IOException("The update helper could not be copied correctly.");
            using(var parent=Process.GetCurrentProcess())
            {
                var plan=new UpdateInstallPlan{ParentPid=parent.Id,ParentStartedUtcTicks=parent.StartTime.ToUniversalTime().Ticks,AppPath=app,CurrentVersion=AppVersion.Number,TargetVersion=choice.Package.Version,PackagePath=choice.Path,Envelope=choice.Verified.Envelope,Rollback=choice.Rollback};
                UpdateInstallPlan.Validate(plan,service.Trust,_store.RootFolder);
                string planPath=Path.Combine(folder,"plan.json");UpdateState.Atomic(planPath,new JavaScriptSerializer().Serialize(plan));
                using(var held=new FileStream(helper,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var process=Process.Start(new ProcessStartInfo(helper,"\""+planPath+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder}))
                {
                    for(int i=0;i<600;i++)
                    {
                        if(File.Exists(Path.Combine(folder,"ready.json")))return;
                        if(process.HasExited)throw new IOException("Update preparation did not finish. Harbor remains open; the installed version was not changed.");
                        await Task.Delay(100);
                    }
                    if(!process.HasExited)process.Kill();
                    throw new TimeoutException("Update preparation timed out. Harbor remains open.");
                }
            }
        }
    }
}
