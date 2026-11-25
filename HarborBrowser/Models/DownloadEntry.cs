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
        private bool _active,_resume,_resumePending;
        private string _interruptReason;
        public string FileName { get {return _fileName;} set {_fileName=value;Changed();} }
        public string FilePath { get {return _filePath;} set {_filePath=value;Changed();} }
        public string Status { get {return _status;} set {_status=value;Changed();} }
        public long BytesReceived { get {return _received;} set {_received=value;Changed();} }
        public long? TotalBytes { get {return _total;} set {_total=value;Changed();} }
        public bool IsActive { get {return _active;} set {_active=value;Changed();} }
        public bool CanResume { get {return _resume;} set {_resume=value;Changed();} }
        public bool ResumePending { get {return _resumePending;} set {_resumePending=value;Changed();} }
        public string InterruptReason { get {return _interruptReason;} set {_interruptReason=value;Changed();} }
        public bool CanRequestResume {get{return CanResume&&!IsActive&&!ResumePending;}}
        public string RecoveryText
        {
            get
            {
                if(ResumePending)return "Resume requested. Waiting for the browser and connection check; nothing restarts automatically after a connection change.";
                if(IsActive||Status=="Complete")return string.Empty;
                if(CanResume)return "Resume is available in this browser session. The website may restart the transfer from the beginning.";
                if(InterruptReason=="UserCanceled"||Status=="Cancelled")return "Cancelled. Start a new download from the original website if needed.";
                if(InterruptReason=="FileNoSpace")return "There is not enough disk space. Free some space, then download the file again from the original website.";
                if(InterruptReason=="ServerUnauthorized"||InterruptReason=="ServerForbidden")return "The website refused this download or needs sign-in. Return to the original website and request a fresh download.";
                if(InterruptReason=="ServerNoRange"||InterruptReason=="ServerContentLengthMismatch")return "The website cannot continue this partial transfer. Request a new download from the original website.";
                if(InterruptReason=="FileVirusInfected"||InterruptReason=="FileSecurityCheckFailed"||InterruptReason=="FileBlockedByPolicy")return "Windows or the browser blocked this file. Review the security notice before requesting it again.";
                if(Status!=null&&Status.StartsWith("Interrupted:",StringComparison.Ordinal))return "This transfer cannot be resumed. Return to the original website and request a fresh download; expired links may need sign-in again.";
                return string.Empty;
            }
        }
        public bool IsPrivate { get; set; }
        public string PrivateSessionId {get;set;}
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
            foreach(string name in new[]{"CanCancel","CanRemove","CanOpen","CanReveal","CanRequestResume","RecoveryText","IsIndeterminate","Percent","ProgressText"})handler(this,new PropertyChangedEventArgs(name));
        }
        private static string Size(long bytes){double size=Math.Max(0,bytes);string[] units={"B","KB","MB","GB","TB"};int i=0;while(size>=1024&&i<4){size/=1024;i++;}return size.ToString("0.#")+" "+units[i];}
    }
}
