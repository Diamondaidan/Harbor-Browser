using System;
using System.IO;
using System.Web.Script.Serialization;

namespace HarborBrowser.Updates
{
    internal sealed class UpdateInstallPlan
    {
        public int ParentPid {get;set;} public long ParentStartedUtcTicks {get;set;}
        public string AppPath {get;set;} public string CurrentVersion {get;set;} public string TargetVersion {get;set;}
        public string PackagePath {get;set;} public string Envelope {get;set;} public bool Rollback {get;set;}
        internal static UpdatePackage Validate(UpdateInstallPlan plan,UpdateTrust trust,string profile)
        {
            var verified=trust.Verify(plan.Envelope,DateTime.UtcNow);var package=UpdateService.Select(verified,plan.TargetVersion);
            var history=UpdateState.Read(Path.Combine(profile,"Updates","history.json"));
            if(history.Sequence>verified.Manifest.Sequence||(history.Sequence==verified.Manifest.Sequence&&history.Sequence!=0&&history.PayloadHash!=verified.PayloadHash))throw new InvalidDataException("The install plan uses superseded release information.");
            var from=UpdateTrust.VersionOf(plan.CurrentVersion);var to=UpdateTrust.VersionOf(plan.TargetVersion);
            if(plan.Rollback)
            {
                if(verified.Manifest.Previous!=package||verified.Manifest.RollbackFrom!=plan.CurrentVersion||to>=from)throw new InvalidDataException("This recovery version is not approved for the installed release.");
            }
            else if(package!=verified.Manifest.Latest||to<=from)throw new InvalidDataException("Only a newer stable release can be installed as an update.");
            string expected=Path.Combine(profile,"Updates","Downloads",package.Sha256.ToUpperInvariant()+".exe");
            if(!Path.GetFullPath(plan.PackagePath).Equals(Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The installer is outside Harbor's verified update folder.");
            if((File.GetAttributes(plan.PackagePath)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("A redirected installer was refused.");
            return package;
        }
        internal static UpdateInstallPlan Read(string path)
        {
            if(new FileInfo(path).Length>150000)throw new InvalidDataException("Update launch details are too large.");
            return new JavaScriptSerializer{MaxJsonLength=150000,RecursionLimit=16}.Deserialize<UpdateInstallPlan>(File.ReadAllText(path))??throw new InvalidDataException("Invalid update launch details.");
        }
    }
}
