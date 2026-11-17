using System;
using System.IO;
using System.Threading;
[assembly:System.Reflection.AssemblyFileVersion("0.6.0.0")]
internal static class Parent {
 static void Main(string[] args){
  string root=Path.GetFullPath(args[0]);
  using(var locked=new FileStream(Path.Combine(root,"profile.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   File.WriteAllText(Path.Combine(root,"parent-ready"),"owned fixture");
   for(int i=0;i<1500&&!File.Exists(Path.Combine(root,"exit-parent"));i++)Thread.Sleep(10);
  }
 }
}
