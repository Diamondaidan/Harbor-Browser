using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;

namespace HarborBrowser.Services
{
    public static class DownloadFileNameService
    {
        private static readonly Dictionary<string, string> MimeExtensions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "application/zip", ".zip" }, { "application/x-zip-compressed", ".zip" },
                { "application/pdf", ".pdf" }, { "image/png", ".png" },
                { "image/jpeg", ".jpg" }, { "image/gif", ".gif" }, { "image/webp", ".webp" },
                { "image/svg+xml", ".svg" }, { "image/bmp", ".bmp" },
                { "image/x-icon", ".ico" }, { "image/vnd.microsoft.icon", ".ico" },
                { "text/plain", ".txt" }, { "text/csv", ".csv" },
                { "application/json", ".json" }, { "application/gzip", ".gz" },
                { "application/x-gzip", ".gz" }, { "application/x-7z-compressed", ".7z" },
                { "application/vnd.rar", ".rar" }, { "application/x-rar-compressed", ".rar" },
                { "audio/mpeg", ".mp3" }, { "video/mp4", ".mp4" },
                { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
                { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
                { "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx" }
            };

        public static string Suggest(string contentDisposition, string suggestedPath, string url, string mimeType)
        {
            string name = null;
            ContentDispositionHeaderValue disposition;
            if (!string.IsNullOrWhiteSpace(contentDisposition) &&
                ContentDispositionHeaderValue.TryParse(contentDisposition, out disposition))
            {
                // FileNameStar is decoded by the framework, including RFC 5987 charset/language.
                name = !string.IsNullOrWhiteSpace(disposition.FileNameStar)
                    ? disposition.FileNameStar : disposition.FileName;
                if (name != null) name = name.Trim().Trim('"');
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                string browserName = Leaf(suggestedPath);
                Uri uri;
                string urlName = Uri.TryCreate(url, UriKind.Absolute, out uri)
                    ? Leaf(uri.LocalPath) : null;
                // Keep WebView2's suggested extension instead of discarding it for a UUID URL.
                name = !string.IsNullOrWhiteSpace(browserName) &&
                    (!string.IsNullOrEmpty(Path.GetExtension(browserName)) || string.IsNullOrWhiteSpace(urlName))
                    ? browserName : urlName;
            }

            name = Clean(name);
            string mime = (mimeType ?? "").Split(';')[0].Trim();
            string extension;
            if (NeedsExtension(name) && MimeExtensions.TryGetValue(mime, out extension))
                name = ReplaceGenericExtension(name, extension);
            return Clean(name);
        }

        private static string Leaf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            // Strip directory components before cleaning invalid filename characters.
            name = name.Replace('\\', '/');
            return name.Substring(name.LastIndexOf('/') + 1);
        }

        public static string Clean(string name)
        {
            name = Leaf(name) ?? "download";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            name = name.Trim().TrimEnd('.', ' ');
            if (string.IsNullOrWhiteSpace(name)) name = "download";
            string device = name.Split('.')[0].TrimEnd(' ', '.').ToUpperInvariant();
            bool numberedDevice = device.Length == 4 &&
                (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal)) &&
                "123456789¹²³".IndexOf(device[3]) >= 0;
            if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" ||
                device == "CONIN$" || device == "CONOUT$" || numberedDevice)
                name = "_" + name;
            if (name.Length > 160)
            {
                string extension = Path.GetExtension(name);
                if (extension.Length > 20) extension = "";
                name = name.Substring(0, 160 - extension.Length).TrimEnd('.', ' ') + extension;
            }
            return name;
        }

        public static string MakeUnique(string folder, string fileName, IEnumerable<string> reservedPaths)
        {
            string cleaned = Clean(fileName);
            var reserved = new HashSet<string>(reservedPaths ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            string stem = Path.GetFileNameWithoutExtension(cleaned);
            string extension = Path.GetExtension(cleaned);
            string candidate = cleaned;
            int suffix = 2;
            while (File.Exists(Path.Combine(folder, candidate)) ||
                   Directory.Exists(Path.Combine(folder, candidate)) ||
                   reserved.Contains(Path.Combine(folder, candidate)))
                candidate = stem + " (" + suffix++ + ")" + extension;
            return candidate;
        }

        public static bool NeedsExtension(string name)
        {
            string extension = Path.GetExtension(name);
            return string.IsNullOrEmpty(extension) ||
                extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".download", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReplaceGenericExtension(string name, string extension)
        {
            return string.IsNullOrEmpty(Path.GetExtension(name)) ? name + extension :
                Path.GetFileNameWithoutExtension(name) + extension;
        }

        public static string DetectExtension(string filePath)
        {
            byte[] bytes = new byte[512];
            int length;
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                length = file.Read(bytes, 0, bytes.Length);
            if (Starts(bytes, length, 0x50, 0x4b, 0x03, 0x04) ||
                Starts(bytes, length, 0x50, 0x4b, 0x05, 0x06) ||
                Starts(bytes, length, 0x50, 0x4b, 0x07, 0x08))
            {
                // Office and several other formats are ZIP containers; retain their actual type.
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    if (archive.GetEntry("[Content_Types].xml") != null)
                    {
                        if (archive.GetEntry("word/document.xml") != null) return ".docx";
                        if (archive.GetEntry("xl/workbook.xml") != null) return ".xlsx";
                        if (archive.GetEntry("ppt/presentation.xml") != null) return ".pptx";
                    }
                    if (archive.GetEntry("AndroidManifest.xml") != null) return ".apk";
                    if (archive.GetEntry("META-INF/MANIFEST.MF") != null) return ".jar";
                    var mimetype = archive.GetEntry("mimetype");
                    if (mimetype != null && mimetype.Length <= 128)
                        using (var reader = new StreamReader(mimetype.Open()))
                            if (reader.ReadToEnd() == "application/epub+zip") return ".epub";
                }
                return ".zip";
            }
            if (Starts(bytes, length, 0x25, 0x50, 0x44, 0x46, 0x2d)) return ".pdf";
            if (Starts(bytes, length, 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a)) return ".png";
            if (Starts(bytes, length, 0xff, 0xd8, 0xff)) return ".jpg";
            if (Starts(bytes, length, 0x47, 0x49, 0x46, 0x38) && length >= 6 &&
                (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61) return ".gif";
            if (Starts(bytes, length, 0x52, 0x49, 0x46, 0x46) && length >= 12 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return ".webp";
            if (Starts(bytes, length, 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c)) return ".7z";
            if (Starts(bytes, length, 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07)) return ".rar";
            if (Starts(bytes, length, 0x1f, 0x8b)) return ".gz";
            // Only infer .exe for a PE file, rather than any file with an MZ prefix.
            if (Starts(bytes, length, 0x4d, 0x5a) && length >= 64)
            {
                int peOffset = BitConverter.ToInt32(bytes, 0x3c);
                using (var file = File.OpenRead(filePath))
                {
                    if (peOffset >= 64 && peOffset <= file.Length - 24)
                    {
                        file.Position = peOffset;
                        byte[] header = new byte[24];
                        if (file.Read(header, 0, header.Length) == header.Length &&
                            Starts(header, header.Length, 0x50, 0x45, 0, 0) &&
                            (BitConverter.ToUInt16(header, 22) & 0x2000) == 0)
                            return ".exe";
                    }
                }
            }
            return null;
        }

        private static bool Starts(byte[] data, int length, params byte[] signature)
        {
            if (length < signature.Length) return false;
            for (int i = 0; i < signature.Length; i++)
                if (data[i] != signature[i]) return false;
            return true;
        }

        public static string RepairCompletedFile(string path, IEnumerable<string> reservedPaths)
        {
            if (!NeedsExtension(path)) return path;
            string extension = DetectExtension(path);
            if (string.IsNullOrEmpty(extension)) return path;
            string folder = Path.GetDirectoryName(path);
            string preferred = ReplaceGenericExtension(Path.GetFileName(path), extension);
            // File.Move never overwrites. Retry if another download wins the target name.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                string target = Path.Combine(folder, MakeUnique(folder, preferred, reservedPaths));
                try { File.Move(path, target); return target; }
                catch (IOException)
                {
                    if (!File.Exists(target) && !Directory.Exists(target)) throw;
                }
            }
            throw new IOException("Could not reserve a filename for the completed download.");
        }
    }
}
