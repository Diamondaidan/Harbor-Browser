using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace HarborBrowser.Models
{
    public sealed class TabResourceRow : INotifyPropertyChanged
    {
        public Guid Id {get;set;}
        public string Title {get;set;}
        public string Scope {get;set;}
        public string State {get;set;}
        public bool KeepAwake {get;set;}
        public bool CanSleep {get;set;}
        public bool CanWake {get;set;}
        public event PropertyChangedEventHandler PropertyChanged;
        public void Update(TabResourceRow row)
        {
            Title=row.Title;Scope=row.Scope;State=row.State;KeepAwake=row.KeepAwake;CanSleep=row.CanSleep;CanWake=row.CanWake;
            PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(string.Empty));
        }
    }
    public sealed class ResourceMonitorActions
    {
        public Func<IList<TabResourceRow>> Tabs {get;set;}
        public Action<Guid> Activate {get;set;}
        public Func<Guid,Task> Sleep {get;set;}
        public Action<Guid> Wake {get;set;}
        public Action<Guid,bool> KeepAwake {get;set;}
        public Func<Task> SleepInactive {get;set;}
    }
}
