using System;
using System.IO;
[assembly:System.Reflection.AssemblyFileVersion("0.6.1.0")]
internal static class Installer {
 static void Main(){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-installed.txt"),"Only an owned fixture ran; no installed app or registry was changed.");}
}
