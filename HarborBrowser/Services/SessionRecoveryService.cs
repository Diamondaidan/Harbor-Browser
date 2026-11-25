using System;
using System.IO;
using System.Linq;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public sealed class SessionRecoveryService : IDisposable
    {
        private readonly BrowserDataStore store;
        private FileStream profileLock;
        public bool Ready {get;private set;}
        public SessionRecoverySnapshot Snapshot {get;private set;}
        public bool HasPending {get{return Snapshot!=null&&Snapshot.Pending&&Snapshot.Session!=null&&Snapshot.Session.Tabs!=null&&Snapshot.Session.Tabs.Count>0;}}
        public SessionRecoveryService(BrowserDataStore data){store=data;}
        public void Begin()
        {
            if(Ready||store.IsReadOnlyForNewerDataVersion)return;
            profileLock=new FileStream(Path.Combine(store.RootFolder,"profile.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            try
            {
                Snapshot=store.LoadRecovery();var previous=store.LoadRunState();var session=store.LoadSession();
                if(previous.Running&&session!=null&&session.Tabs!=null&&session.Tabs.Count>0)
                {
                    bool meaningful=session.Tabs.Any(t=>t!=null&&!HomePageService.IsHomeUrl(t.Url));
                    if(!HasPending||meaningful){Snapshot=new SessionRecoverySnapshot{Session=session,Pending=true,CapturedUtc=DateTime.UtcNow};store.SaveRecovery(Snapshot);}
                }
                store.SaveRunState(new SessionRunState{Running=true,StartedUtc=DateTime.UtcNow});Ready=true;
            }
            catch{profileLock.Dispose();profileLock=null;throw;}
        }
        public void Dismiss(){if(Snapshot==null)return;Snapshot.Pending=false;store.SaveRecovery(Snapshot);}
        public void Clear(){store.ResetRecoveryFiles();Snapshot=new SessionRecoverySnapshot();}
        public void MarkClean(){if(Ready)store.SaveRunState(new SessionRunState{Running=false,StartedUtc=DateTime.UtcNow});}
        public void Dispose(){Ready=false;if(profileLock!=null){profileLock.Dispose();profileLock=null;}}
    }
}
