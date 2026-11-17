using System;
using System.IO;
using System.Web.Script.Serialization;

namespace HarborBrowser.Updates
{
    public sealed class UpdateState
    {
        public long Sequence {get;set;} public string PayloadHash {get;set;} public string Version {get;set;}
        public DateTime LastCheckUtc {get;set;}
        internal static UpdateState Read(string path)
        {
            if(!File.Exists(path))return new UpdateState();
            try{if(new FileInfo(path).Length>8192)throw new IOException();return new JavaScriptSerializer().Deserialize<UpdateState>(File.ReadAllText(path))??new UpdateState();}
            catch{throw new InvalidDataException("Saved update history is damaged. Updates are paused; keep this file for recovery.");}
        }
        internal static void Accept(string path,VerifiedUpdate update,DateTime now)
        {
            var old=Read(path);var m=update.Manifest;
            if(old.LastCheckUtc>now.AddMinutes(5)||m.Sequence<old.Sequence||(m.Sequence==old.Sequence&&old.Sequence!=0&&old.PayloadHash!=update.PayloadHash)||(!string.IsNullOrEmpty(old.Version)&&UpdateTrust.VersionOf(m.Latest.Version)<UpdateTrust.VersionOf(old.Version)))throw new InvalidDataException("An older or inconsistent release record was refused.");
            Atomic(path,new JavaScriptSerializer().Serialize(new UpdateState{Sequence=m.Sequence,PayloadHash=update.PayloadHash,Version=m.Latest.Version,LastCheckUtc=now}));
        }
        internal static void Atomic(string path,string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=Path.Combine(Path.GetDirectoryName(path),"."+Guid.NewGuid().ToString("N")+".tmp");
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false))){writer.Write(text);writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        internal static void SafeDirectory(string path)
        {
            Directory.CreateDirectory(path);
            for(var entry=new DirectoryInfo(Path.GetFullPath(path));entry!=null;entry=entry.Parent)if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Updates cannot use a redirected storage folder.");
        }
    }
}
