using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    // A user-scoped pipe forwards Windows link activations to the existing UI.
    public sealed class BrowserInstanceService:IDisposable
    {
        private readonly string _name;
        private readonly Mutex _mutex;
        private readonly SecurityIdentifier _user;
        private volatile bool _disposed;
        private NamedPipeServerStream _server;
        public bool IsPrimary {get;private set;}
        public BrowserInstanceService(string suffix="")
        {
            _user=WindowsIdentity.GetCurrent().User;
            _name="HarborBrowser."+_user.Value+suffix;
            _mutex=new Mutex(false,@"Local\"+_name);
            try{IsPrimary=_mutex.WaitOne(0);}catch(AbandonedMutexException){IsPrimary=true;}
        }
        public async Task Listen(Action<string[]> receive)
        {
            if(!IsPrimary)throw new InvalidOperationException("Only the primary browser can listen.");
            while(!_disposed)
            {
                try
                {
                    var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(_user,PipeAccessRights.FullControl,AccessControlType.Allow));
                    using(var server=new NamedPipeServerStream(_name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,65536,65536,security))
                    {
                        _server=server;await Task.Factory.FromAsync(server.BeginWaitForConnection,server.EndWaitForConnection,null);
                        using(var timeout=new Timer(s=>{try{server.Dispose();}catch{}},null,5000,Timeout.Infinite))
                        {
                            byte[] length=new byte[4];await ReadAll(server,length);int count=BitConverter.ToInt32(length,0);if(count<2||count>65536)throw new InvalidDataException("Invalid link activation.");
                            byte[] bytes=new byte[count];await ReadAll(server,bytes);
                            string[] values=new JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=4}.Deserialize<string[]>(Encoding.UTF8.GetString(bytes));
                            var targets=ExternalNavigationService.Targets(values);receive(targets.ToArray());await server.WriteAsync(new byte[]{1},0,1);
                        }
                    }
                }
                catch(Exception ex){if(!_disposed)System.Diagnostics.Debug.WriteLine("Link activation unavailable: "+ex.Message);}
                finally{_server=null;}
            }
        }
        public bool Forward(string[] targets,int milliseconds=5000)
        {
            try
            {
                var checkedTargets=ExternalNavigationService.Targets(targets);
                byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(checkedTargets));if(bytes.Length>65536)return false;
                using(var pipe=new NamedPipeClientStream(".",_name,PipeDirection.InOut,PipeOptions.Asynchronous))
                {
                    pipe.Connect(milliseconds);
                    using(var timer=new Timer(s=>{try{pipe.Dispose();}catch{}},null,milliseconds,Timeout.Infinite))
                    {
                        pipe.Write(BitConverter.GetBytes(bytes.Length),0,4);pipe.Write(bytes,0,bytes.Length);pipe.Flush();return pipe.ReadByte()==1;
                    }
                }
            }
            catch{return false;}
        }
        private static async Task ReadAll(Stream stream,byte[] bytes)
        {int offset=0;while(offset<bytes.Length){int count=await stream.ReadAsync(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}}
        public void Dispose(){if(_disposed)return;_disposed=true;try{_server?.Dispose();}catch{}if(IsPrimary){_mutex.ReleaseMutex();IsPrimary=false;}_mutex.Dispose();}
    }
}
