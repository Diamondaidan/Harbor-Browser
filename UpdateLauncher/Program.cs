using System;
using System.IO;
using HarborBrowser.Updates;
internal static class Program {
 [STAThread]private static int Main(string[] args){
  if(args.Length!=1)return 2;
  string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HarborBrowser");
  return UpdateInstallRunner.Run(args[0],profile,UpdateTrustRoot.Create(),message=>System.Windows.Forms.MessageBox.Show(message,"Harbor update",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Information));
 }
}
