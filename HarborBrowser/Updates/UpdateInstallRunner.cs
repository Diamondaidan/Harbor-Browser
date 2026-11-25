using System;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;
namespace HarborBrowser.Updates {
 internal static class UpdateInstallRunner {
 internal static int Run(string planArgument,string profile,UpdateTrust trust,Action<string> notify=null) {
        string result=null;bool started=false,parentClosed=false;
        try
        {
            
            
            string launchRoot=Path.Combine(profile,"Updates","Launch");UpdateState.SafeDirectory(launchRoot);
            string planPath=Path.GetFullPath(planArgument);
            if(!planPath.StartsWith(launchRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(planPath)!="plan.json")return 2;
            string folder=Path.GetDirectoryName(planPath);UpdateState.SafeDirectory(folder);result=Path.Combine(folder,"result.json");
            var plan=UpdateInstallPlan.Read(planPath);var package=UpdateInstallPlan.Validate(plan,trust,profile);
            string app=BuildIdentity.LocalPath(plan.AppPath);
            if(Path.GetFileName(app)!="HarborBrowser.exe"||FileVersionInfo.GetVersionInfo(app).FileVersion!=plan.CurrentVersion+".0")throw new InvalidDataException("The update launcher does not match the installed browser.");
            UpdateState.SafeDirectory(Path.GetDirectoryName(plan.PackagePath));
            using(var parent=Process.GetProcessById(plan.ParentPid))
            using(var installer=new FileStream(plan.PackagePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(parent.StartTime.ToUniversalTime().Ticks!=plan.ParentStartedUtcTicks||!Path.GetFullPath(parent.MainModule.FileName).Equals(app,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The original Harbor process could not be verified.");
                UpdateService.VerifyStream(installer,package);
                if(FileVersionInfo.GetVersionInfo(plan.PackagePath).FileVersion?.Trim()!=package.Version+".0")throw new InvalidDataException("The installer file version did not match the approved release.");
                if(!string.IsNullOrEmpty(package.CertificateSha256))UpdateAuthenticode.Verify(plan.PackagePath,package.CertificateSha256);
                UpdateState.Atomic(Path.Combine(folder,"ready.json"),"{\"Ready\":true}");
                if(!parent.WaitForExit(120000))throw new IOException("Harbor did not close. No update was installed.");
                parentClosed=true;
                UpdateRecovery.Snapshot(profile,plan.CurrentVersion,plan.TargetVersion);
                // Waiting for shutdown or copying a large snapshot can cross the
                // record's expiry. Recheck immediately before opening setup.
                UpdateInstallPlan.Validate(plan,trust,profile);
                // Keep the installer read-locked through launch and completion,
                // including Windows' approval prompt. No silent setup or reboot.
                using(var setup=Process.Start(new ProcessStartInfo(plan.PackagePath){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(plan.PackagePath)}))
                {
                    if(setup==null)throw new IOException("Windows did not return an installer process.");
                    started=true;
                    setup.WaitForExit();UpdateState.Atomic(result,new JavaScriptSerializer().Serialize(new{Started=true,ExitCode=setup.ExitCode,TargetVersion=plan.TargetVersion,CompletedUtc=DateTime.UtcNow.ToString("o")}));
                }
            }
            return 0;
        }
        catch
        {
            string message=started?"Setup was opened. Review its status before retrying.":"Update preparation or Windows approval did not finish. Setup was not opened; the installed browser was kept. Open Harbor again to review Updates and recovery.";
            if(result!=null)try{UpdateState.Atomic(result,new JavaScriptSerializer().Serialize(new{Started=started,Message=message}));}catch{}
            if(parentClosed)try{notify?.Invoke(message);}catch{}
            return 1;
        }

 }
 }
}
