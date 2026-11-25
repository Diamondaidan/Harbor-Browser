using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HarborBrowser.Services
{
    public static class VpnService
    {
        public static List<string> GetConfiguredProfiles()
        {
            var profiles = new List<string>();
            foreach (string path in GetPhonebookPaths())
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    foreach (string rawLine in File.ReadAllLines(path))
                    {
                        string line = rawLine.Trim();
                        if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                        {
                            string name = line.Substring(1, line.Length - 2).Trim();
                            if (name.Length > 0 && !name.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) &&
                                !profiles.Contains(name, StringComparer.OrdinalIgnoreCase))
                                profiles.Add(name);
                        }
                    }
                }
                catch
                {
                    // An inaccessible phonebook is skipped; Windows Settings remains available.
                }
            }
            profiles.Sort(StringComparer.OrdinalIgnoreCase);
            return profiles;
        }

        private static IEnumerable<string> GetPhonebookPaths()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            yield return Path.Combine(appData, "Microsoft", "Network", "Connections", "Pbk", "rasphone.pbk");
            yield return Path.Combine(programData, "Microsoft", "Network", "Connections", "Pbk", "rasphone.pbk");
        }

        public static string Connect(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return "Choose a VPN profile first.";

            return RunRasDial("\"" + profileName.Replace("\"", string.Empty) + "\"");
        }

        public static string Disconnect()
        {
            return RunRasDial("/disconnect");
        }

        private static string RunRasDial(string arguments)
        {
            string rasdial = Path.Combine(Environment.SystemDirectory, "rasdial.exe");
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = rasdial,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(start))
                {
                    if (process == null)
                        return "Windows did not start rasdial.exe.";

                    if (!process.WaitForExit(30000))
                    {
                        try { process.Kill(); } catch { }
                        return "The VPN command timed out. Check the connection in Windows Settings.";
                    }

                    string output = (process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd()).Trim();
                    if (process.ExitCode == 0)
                        return string.IsNullOrWhiteSpace(output) ? "VPN command completed." : output;

                    return "ERROR: " + (string.IsNullOrWhiteSpace(output)
                        ? "Windows could not complete the VPN command. Check the profile and saved credentials."
                        : output);
                }
            }
            catch (Exception ex)
            {
                return "VPN command failed: " + ex.Message;
            }
        }

        public static void OpenWindowsVpnSettings()
        {
            Process.Start(new ProcessStartInfo("ms-settings:network-vpn") { UseShellExecute = true });
        }
    }
}
