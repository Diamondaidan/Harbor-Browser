using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public sealed class BrowserDataStore
    {
        private const int CurrentDataSchemaVersion = 5;
        private const int MaximumAutomaticBackups = 5;

        public string RootFolder { get; private set; }
        public string DownloadsFolder { get; private set; }
        public string BackupsFolder { get; private set; }
        public bool IsReadOnlyForNewerDataVersion { get; private set; }

        public BrowserDataStore() : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HarborBrowser"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Harbor Browser")) { }

        public BrowserDataStore(string rootFolder,string downloadsFolder)
        {
            RootFolder=Path.GetFullPath(rootFolder);
            DownloadsFolder=Path.GetFullPath(downloadsFolder);
            BackupsFolder=Path.Combine(RootFolder,"Backups");
            Directory.CreateDirectory(RootFolder);Directory.CreateDirectory(DownloadsFolder);EnsureDataSchema();
        }
        public BrowserSettings LoadSettings()
        {
            return Load("settings.json", new BrowserSettings());
        }

        public List<Bookmark> LoadBookmarks()
        {
            return Load("bookmarks.json", new List<Bookmark>());
        }

        public List<HistoryEntry> LoadHistory()
        {
            return Load("history.json", new List<HistoryEntry>());
        }

        public BrowserSession LoadSession()
        {
            return Load("session.json", new BrowserSession());
        }

        public void SaveSession(BrowserSession session) { Save("session.json", session ?? new BrowserSession()); }

        public void SaveSettings(BrowserSettings settings) { Save("settings.json", settings); }
        public void SaveBookmarks(List<Bookmark> bookmarks) { Save("bookmarks.json", bookmarks); }
        public void SaveHistory(List<HistoryEntry> history) { Save("history.json", history); }

        private void EnsureDataSchema()
        {
            string versionPath = Path.Combine(RootFolder, "data-version.txt");
            int storedVersion = 0;
            bool backupAlreadyCreated = false;

            if (File.Exists(versionPath))
            {
                if (!int.TryParse(File.ReadAllText(versionPath).Trim(), out storedVersion))
                {
                    CreateBackupSnapshot("invalid-version-marker");
                    backupAlreadyCreated = true;
                    storedVersion = 0;
                }
            }

            // Never let an older Harbor build rewrite data produced by a newer schema.
            if (storedVersion > CurrentDataSchemaVersion)
            {
                IsReadOnlyForNewerDataVersion = true;
                return;
            }

            if (storedVersion < CurrentDataSchemaVersion)
            {
                // Schema 5 adds persistent custom tab groups to the saved session.
                // Existing bookmarks, history, settings, and tab data remain readable.
                // Snapshot the profile before migration.
                if (!backupAlreadyCreated)
                    CreateBackupSnapshot("before-schema-5");
                WriteTextAtomically(versionPath, CurrentDataSchemaVersion.ToString());
            }
        }

        private string CreateBackupSnapshot(string reason)
        {
            string[] fileNames =
            {
                "settings.json", "settings.json.bak",
                "bookmarks.json", "bookmarks.json.bak",
                "history.json", "history.json.bak",
                "session.json", "session.json.bak",
                "data-version.txt", "data-version.txt.bak"
            };

            if (!fileNames.Any(name => File.Exists(Path.Combine(RootFolder, name))))
                return null;

            Directory.CreateDirectory(BackupsFolder);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupPath = Path.Combine(BackupsFolder, stamp + "-" + reason);
            int suffix = 1;
            while (Directory.Exists(backupPath))
            {
                backupPath = Path.Combine(BackupsFolder, stamp + "-" + reason + "-" + suffix);
                suffix++;
            }

            Directory.CreateDirectory(backupPath);
            foreach (string name in fileNames)
            {
                string source = Path.Combine(RootFolder, name);
                if (File.Exists(source))
                    File.Copy(source, Path.Combine(backupPath, name), true);
            }

            PruneAutomaticBackups();
            return backupPath;
        }

        private void PruneAutomaticBackups()
        {
            if (!Directory.Exists(BackupsFolder))
                return;

            DirectoryInfo[] backups = new DirectoryInfo(BackupsFolder).GetDirectories()
                .Where(d => d.Name.Contains("-before-schema-") || d.Name.Contains("-invalid-version-marker"))
                .OrderByDescending(d => d.CreationTimeUtc)
                .ToArray();

            for (int i = MaximumAutomaticBackups; i < backups.Length; i++)
            {
                try { backups[i].Delete(true); }
                catch { /* A backup in use is retained; it is safer not to block startup. */ }
            }
        }

        private static void WriteTextAtomically(string path, string contents)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                string backupPath = path + ".bak";
                try
                {
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Replace(tempPath, path, backupPath, true);
                }
                catch (IOException)
                {
                    File.Copy(path, backupPath, true);
                    File.Copy(tempPath, path, true);
                    File.Delete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private T Load<T>(string fileName, T fallback)
        {
            string path = Path.Combine(RootFolder, fileName);
            if (!File.Exists(path))
                return fallback;

            T result;
            if (TryDeserialize(path, out result))
                return result;

            // If the latest preferences file was interrupted or damaged, recover the
            // previous known-good copy instead of silently starting with empty data.
            string backupPath = path + ".bak";
            if (File.Exists(backupPath) && TryDeserialize(backupPath, out result))
            {
                try { File.Copy(backupPath, path, true); }
                catch { /* The recovered value is still usable for this session. */ }
                return result;
            }

            return fallback;
        }

        private static bool TryDeserialize<T>(string path, out T result)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                result = serializer.Deserialize<T>(File.ReadAllText(path));
                return result != null;
            }
            catch
            {
                result = default(T);
                return false;
            }
        }

        private void Save<T>(string fileName, T value)
        {
            if (IsReadOnlyForNewerDataVersion)
                throw new InvalidOperationException("This Harbor profile was created by a newer version. Saving is disabled to protect your data. Install the newer Harbor version to make changes.");

            Directory.CreateDirectory(RootFolder);
            string path = Path.Combine(RootFolder, fileName);
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string json = serializer.Serialize(value);

            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Replace(tempPath, path, backupPath, true);
            }
            catch (IOException)
            {
                // Fallback for filesystems that do not support Replace. Keep a copy
                // of the old file before overwriting it; Load() can recover from it.
                File.Copy(path, backupPath, true);
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }
        }
    }
}
