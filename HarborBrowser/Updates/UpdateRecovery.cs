using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace HarborBrowser.Updates
{
    internal static class UpdateRecovery
    {
        internal static readonly string[] Names={"settings.json","bookmarks.json","history.json","session.json","permissions.json","session-recovery.json","run-state.json","data-version.txt","modules.json","module-permissions.json","proton-preview.txt","browser-protection.txt"};
        internal static string Snapshot(string profile,string from,string to)
        {
            UpdateState.SafeDirectory(profile);
            string version=Path.Combine(profile,"data-version.txt");
            if(!File.Exists(version)||File.ReadAllText(version).Trim()!="9")throw new InvalidDataException("This profile's data format is not compatible with the selected release.");
            // Call only after the browser exits. Holding this same profile lock
            // prevents another Harbor process from writing during the snapshot.
            using(var profileLock=new FileStream(Path.Combine(profile,"profile.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                string parent=Path.Combine(profile,"UpgradeBackups");UpdateState.SafeDirectory(parent);
                string folder=Path.Combine(parent,"update-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
                var files=new List<object>();
                foreach(string name in Names)foreach(string suffix in new[]{"",".bak"})
                {
                    string source=Path.Combine(profile,name+suffix);if(!File.Exists(source))continue;
                    if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("A redirected profile data file cannot be backed up.");
                    string target=Path.Combine(folder,name+suffix);File.Copy(source,target,false);
                    files.Add(new{File=name+suffix,Bytes=new FileInfo(target).Length,Sha256=UpdateTrust.HashFile(target)});
                }
                UpdateState.Atomic(Path.Combine(folder,"snapshot.json"),new JavaScriptSerializer().Serialize(new{Schema=1,DataSchema=9,From=from,To=to,CreatedUtc=DateTime.UtcNow.ToString("o"),AuthenticationCopied=false,Files=files}));
                return folder;
            }
        }
    }
}
