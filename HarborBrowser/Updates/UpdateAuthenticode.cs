using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace HarborBrowser.Updates
{
    internal static class UpdateAuthenticode
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private sealed class FileInfoNative
        { internal uint Size=(uint)Marshal.SizeOf(typeof(FileInfoNative));[MarshalAs(UnmanagedType.LPWStr)]internal string Path;internal IntPtr FileHandle,KnownSubject; }
        [StructLayout(LayoutKind.Sequential)]private sealed class TrustData
        {internal uint Size=(uint)Marshal.SizeOf(typeof(TrustData));internal IntPtr Policy,Sip;internal uint Ui=2,Revocation=1,Choice=1;internal IntPtr File;internal uint State;internal IntPtr StateData;internal IntPtr Url;internal uint Flags=0x1080,Context;internal IntPtr SignatureSettings;}
        [DllImport("wintrust.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern int WinVerifyTrust(IntPtr window,[In]ref Guid action,[In,Out]TrustData data);
        internal static void Verify(string path,string certificateHash)
        {
            var file=new FileInfoNative{Path=path};var data=new TrustData();data.File=Marshal.AllocHGlobal(Marshal.SizeOf(file));
            try
            {
                Marshal.StructureToPtr(file,data.File,false);var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
                // No trust prompts or background network access. If cached Windows
                // trust/revocation evidence is insufficient, verification fails.
                int result=WinVerifyTrust(new IntPtr(-1),ref action,data);
                if(result!=0)throw new System.IO.InvalidDataException("Windows could not verify the installer's code signature.");
                using(var certificate=new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                    if(!UpdateTrust.Hash(certificate.RawData).Equals(certificateHash,StringComparison.OrdinalIgnoreCase))throw new System.IO.InvalidDataException("The installer publisher certificate did not match the signed release record.");
            }
            finally{Marshal.DestroyStructure(data.File,typeof(FileInfoNative));Marshal.FreeHGlobal(data.File);}
        }
    }
}
