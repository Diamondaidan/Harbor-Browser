using Microsoft.Web.WebView2.Core;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HarborBrowser.Models
{
    public sealed class DownloadEntry : INotifyPropertyChanged
    {
        private string _fileName,_filePath,_status;
        private long _received;
        private long? _total;
        private bool _active,_resume;
        public string FileName { get {return _fileName;} set {_fileName=value;Changed();} }
        public string FilePath { get {return _filePath;} set {_filePath=value;Changed();} }
        public string Status { get {return _status;} set {_status=value;Changed();} }
        public long BytesReceived { get {return _received;} set {_received=value;Changed();} }
        public long? TotalBytes { get {return _total;} set {_total=value;Changed();} }
        public bool IsActive { get {return _active;} set {_active=value;Changed();} }
        public bool CanResume { get {return _resume;} set {_resume=value;Changed();} }
        public bool IsPrivate { get; set; }
        public string SourceUrl { get; set; }
        public bool CanCancel { get {return IsActive || CanResume;} }
        public bool CanRemove { get {return !IsActive&&!CanResume;} }
        public bool CanOpen { get {return Status=="Complete"&&File.Exists(FilePath);} }
        public bool CanReveal { get {return File.Exists(FilePath);} }
        public bool IsIndeterminate { get {return IsActive&&(!TotalBytes.HasValue||TotalBytes.Value<=0);} }
        public double Percent { get {return TotalBytes.GetValueOrDefault()>0?Math.Min(100,Math.Max(0,BytesReceived*100.0/TotalBytes.Value)):0;} }
        public string ProgressText {get{return (Status??"Starting")+"  ·  "+Size(BytesReceived)+(TotalBytes.GetValueOrDefault()>0?" of "+Size(TotalBytes.Value):string.Empty);}}
        public CoreWebView2DownloadOperation Operation { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed([CallerMemberName]string property=null)
        {
            var handler=PropertyChanged;if(handler==null)return;
            handler(this,new PropertyChangedEventArgs(property));
            foreach(string name in new[]{"CanCancel","CanRemove","CanOpen","CanReveal","IsIndeterminate","Percent","ProgressText"})handler(this,new PropertyChangedEventArgs(name));
        }
        private static string Size(long bytes){double size=Math.Max(0,bytes);string[] units={"B","KB","MB","GB","TB"};int i=0;while(size>=1024&&i<4){size/=1024;i++;}return size.ToString("0.#")+" "+units[i];}
    }
}
