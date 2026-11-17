using System;
using System.Collections.Generic;
using System.Linq;

namespace HarborBrowser.Services
{
    public static class TabSleepSchedule
    {
        public static TimeSpan Delay(DateTime inactiveSince,DateTime retryAfter,DateTime now,int seconds)
        {
            DateTime due=inactiveSince.AddSeconds(Math.Max(15,Math.Min(600,seconds)));
            if(retryAfter>due)due=retryAfter;
            return TimeSpan.FromMilliseconds(Math.Max(100,(due-now).TotalMilliseconds));
        }
    }

    public sealed class ProcessCpuSampler
    {
        private sealed class Sample {public long Started,Timestamp;public TimeSpan Cpu;}
        private readonly Dictionary<int,Sample> previous=new Dictionary<int,Sample>();
        public double? SamplePercent(int id,long started,TimeSpan cpu,long timestamp,long frequency,int processors)
        {
            Sample old;previous.TryGetValue(id,out old);previous[id]=new Sample{Started=started,Timestamp=timestamp,Cpu=cpu};
            if(old==null||old.Started!=started||timestamp<=old.Timestamp||cpu<old.Cpu||frequency<=0)return null;
            double seconds=(timestamp-old.Timestamp)/(double)frequency;
            return Math.Max(0,Math.Min(100,(cpu-old.Cpu).TotalSeconds/seconds/Math.Max(1,processors)*100));
        }
        public void Retain(IEnumerable<int> ids)
        {var current=new HashSet<int>(ids);foreach(int id in previous.Keys.Where(k=>!current.Contains(k)).ToList())previous.Remove(id);}
    }
}
