using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    // Optional provider registration only. A registered extension is not proof
    // of sign-in, a VPN connection, or a verified browser route.
    public sealed class ProtonPreviewService : IDisposable
    {
        public const string ExtensionId = "jplgfhpmjnbigmhklmmbgecoobifkmpa";
        public const string ExpectedVersion = "1.3.6";
        public const string PopupUrl = "chrome-extension://" + ExtensionId + "/popup.html";
        public const string AccountUrl = "https://account.proton.me/vpn";

        private readonly string _setting, _registration;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CancellationTokenSource _operation;
        private Task<CoreWebView2BrowserExtension> _pendingRegistration;
        private volatile bool _enabled, _desiredEnabled, _disposed, _explicitRemoval;
        private int _intent;

        public string PackageFolder { get; }
        public bool Enabled { get { return _enabled; } }
        public bool HasSavedRegistration { get { return File.Exists(_registration); } }
        public string LastError { get; private set; }
        public string Status { get; private set; } = "Off · regular browser connection";
        public event EventHandler StatusChanged;
        internal TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);
        internal Func<CoreWebView2Profile, string, Task<CoreWebView2BrowserExtension>> ExtensionAdder;

        public ProtonPreviewService(string profile, string packageFolder = null)
        {
            if (string.IsNullOrWhiteSpace(profile)) throw new ArgumentException("A browser profile is required.", nameof(profile));
            _setting = Path.Combine(profile, "proton-preview.txt");
            _registration = Path.Combine(profile, "proton-preview-registration.txt");
            PackageFolder = Path.GetFullPath(packageFolder ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "BrowserVpn", "Proton"));
            _desiredEnabled = _enabled = File.Exists(_setting) && File.ReadAllText(_setting).Trim() != "off";
            if (Enabled) Status = "Restoring the Proton integration · connection not yet verified";
        }

        public static bool IsProviderPage(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "chrome-extension" && uri.Host == ExtensionId;
        }

        public static bool IsAccountPage(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "https" && (uri.Host == "account.proton.me" || uri.Host == "account.protonvpn.com" || uri.Host == "account-api.proton.me");
        }
        public static bool IsSensitivePage(string value)
        { return IsAccountPage(value) || IsProviderPage(value); }

        public void ValidatePackage()
        {
            string manifest = Path.Combine(PackageFolder, "manifest.json");
            if (!File.Exists(manifest) || new FileInfo(manifest).Length > 65536)
                throw new InvalidDataException("The bundled Proton extension is missing or invalid. Reinstall the complete Harbor package.");
            var data = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 24 }
                .DeserializeObject(File.ReadAllText(manifest)) as Dictionary<string, object>;
            object version, key, manifestVersion;
            if (data == null || !data.TryGetValue("version", out version) || Convert.ToString(version) != ExpectedVersion ||
                !data.TryGetValue("key", out key) || !data.TryGetValue("manifest_version", out manifestVersion) || Convert.ToInt32(manifestVersion) != 3)
                throw new InvalidDataException("The Proton extension version or manifest does not match this Harbor release.");
            byte[] digest;
            try { using (var sha = SHA256.Create()) digest = sha.ComputeHash(Convert.FromBase64String(Convert.ToString(key))); }
            catch (FormatException) { throw new InvalidDataException("The Proton extension key is invalid."); }
            catch (ArgumentException) { throw new InvalidDataException("The Proton extension key is invalid."); }
            string id = string.Concat(digest.Take(16).SelectMany(b => new[] { (char)('a' + (b >> 4)), (char)('a' + (b & 15)) }));
            if (id != ExtensionId) throw new InvalidDataException("The bundled extension identity does not match Proton VPN.");
            if (!File.Exists(Path.Combine(PackageFolder, "popup.html")))
                throw new InvalidDataException("The bundled Proton controls are missing.");
        }

        public Task Prepare(CoreWebView2Profile profile)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProtonPreviewService));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!Enabled || profile.IsInPrivateModeEnabled) return Task.CompletedTask;
            return PrepareCore(profile, Volatile.Read(ref _intent));
        }

        public async Task Enable(CoreWebView2Profile profile)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProtonPreviewService));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.IsInPrivateModeEnabled) throw new InvalidOperationException("Enable Proton browser protection in a normal browser profile.");
            int intent = Interlocked.Increment(ref _intent);
            CancelOperation();
            File.WriteAllText(_setting, "on");
            _explicitRemoval = false; _desiredEnabled = _enabled = true;
            LastError = null;
            SetStatus("Enabling Proton controls · connection not yet verified");
            await PrepareCore(profile, intent);
        }

        private async Task PrepareCore(CoreWebView2Profile profile, int intent)
        {
            await _gate.WaitAsync(_lifetime.Token);
            using (var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                _operation = operation;
                try
                {
                    RequireCurrent(intent, operation.Token); ValidatePackage();
                    await FinishPendingRegistration(operation.Token);
                    RequireCurrent(intent, operation.Token);
                    var extensions = await Deadline(profile.GetBrowserExtensionsAsync(), operation.Token);
                    var extension = extensions.FirstOrDefault(e => e.Id == ExtensionId);
                    // Reuse a current registration instead of repeatedly reinstalling
                    // it while a provider popup or sign-in handoff is active.
                    string packagePath = Path.GetFullPath(PackageFolder);
                    bool registeredHere = File.Exists(_registration) && string.Equals(File.ReadAllText(_registration).Trim(), packagePath, StringComparison.OrdinalIgnoreCase);
                    if (extension == null || !registeredHere)
                    {
                        var adding = ExtensionAdder == null ? profile.AddBrowserExtensionAsync(packagePath) : ExtensionAdder(profile, packagePath);
                        _pendingRegistration = adding;
                        try { extension = await Deadline(adding, operation.Token); }
                        catch { _ = ReconcileLateRegistration(adding); throw; }
                    }
                    RequireCurrent(intent, operation.Token);
                    if (extension.Id != ExtensionId)
                    {
                        await Bounded(extension.EnableAsync(false), CancellationToken.None);
                        await Bounded(extension.RemoveAsync(), CancellationToken.None);
                        _pendingRegistration = null;
                        throw new InvalidDataException("The native Proton extension identity could not be verified.");
                    }
                    if (!extension.IsEnabled) await Bounded(extension.EnableAsync(true), operation.Token);
                    RequireCurrent(intent, operation.Token);
                    File.WriteAllText(_registration, packagePath);
                    _pendingRegistration = null;
                    LastError = null;
                    SetStatus("Proton integration enabled · sign in and connect in Proton's controls; route not yet verified");
                }
                catch (Exception ex)
                {
                    if (Current(intent))
                    {
                        LastError = Friendly(ex);
                        SetStatus("Proton controls need attention. " + LastError + " Turn off protection or remove/reset Proton.");
                    }
                    throw;
                }
                finally { if (ReferenceEquals(_operation, operation)) _operation = null; _gate.Release(); }
            }
        }

        public async Task Disable(CoreWebView2Profile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            int intent = Interlocked.Increment(ref _intent);
            _explicitRemoval = false; _desiredEnabled = false;
            CancelOperation();
            SetStatus("Turning off protection and keeping Proton's saved sign-in…");
            await _gate.WaitAsync();
            try
            {
                if (intent != Volatile.Read(ref _intent) || _desiredEnabled)
                    throw new OperationCanceledException("The Proton change was superseded.");
                await FinishPendingRegistration(CancellationToken.None);
                var extension = (await Deadline(profile.GetBrowserExtensionsAsync(), CancellationToken.None)).FirstOrDefault(e => e.Id == ExtensionId);
                // Disable is persisted by WebView2. Never uninstall or clear
                // provider storage for the ordinary off/on action.
                if (extension != null) await Bounded(extension.EnableAsync(false), CancellationToken.None);
                if (intent != Volatile.Read(ref _intent) || _desiredEnabled)
                    throw new OperationCanceledException("The Proton change was superseded.");
                File.WriteAllText(_setting, "off");
                _enabled = false; LastError = null;
                SetStatus("Off · regular browser connection restored; Proton's saved sign-in is kept");
            }
            catch (Exception ex)
            {
                if (intent == Volatile.Read(ref _intent))
                {
                    LastError = Friendly(ex);
                    SetStatus("Turning off protection did not finish. " + LastError + " Retry before ordinary browsing.");
                }
                throw;
            }
            finally { _gate.Release(); }
        }

        public async Task Remove(CoreWebView2Profile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            int intent = Interlocked.Increment(ref _intent);
            _explicitRemoval = true; _desiredEnabled = false;
            CancelOperation();
            SetStatus("Removing the Proton integration and restoring the regular route…");
            await _gate.WaitAsync();
            try
            {
                // Explicit reversal remains usable after interrupted setup/disposal.
                await FinishPendingRegistration(CancellationToken.None);
                var extension = (await Deadline(profile.GetBrowserExtensionsAsync(), CancellationToken.None)).FirstOrDefault(e => e.Id == ExtensionId);
                if (extension != null)
                {
                    await Bounded(extension.EnableAsync(false), CancellationToken.None);
                    await Bounded(extension.RemoveAsync(), CancellationToken.None);
                }
                if (!_desiredEnabled && (intent == Volatile.Read(ref _intent) || _disposed))
                {
                    File.WriteAllText(_setting, "off");
                    if (File.Exists(_registration)) File.Delete(_registration);
                    _enabled = false; LastError = null;
                    SetStatus("Off · regular browser connection restored");
                }
            }
            catch (Exception ex)
            {
                LastError = Friendly(ex);
                SetStatus("Proton removal did not finish. " + LastError + " Close Harbor and retry removal before ordinary browsing.");
                throw;
            }
            finally { _gate.Release(); }
        }

        // The caller holds the gate. A timed-out/cancelled native add may still
        // complete; don't restore the regular route until it is settled.
        private async Task FinishPendingRegistration(CancellationToken cancellation)
        {
            var pending = _pendingRegistration;
            if (pending == null) return;
            try
            {
                var extension = await Deadline(pending, cancellation);
                if (extension.Id != ExtensionId) throw new InvalidDataException("The pending Proton extension identity did not match.");
                if (!_explicitRemoval) File.WriteAllText(_registration, Path.GetFullPath(PackageFolder));
            }
            catch (Exception) when (pending.IsFaulted || pending.IsCanceled) { }
            finally { if (pending.IsCompleted && ReferenceEquals(_pendingRegistration, pending)) _pendingRegistration = null; }
        }

        private bool Current(int intent) { return !_disposed && _desiredEnabled && _enabled && intent == Volatile.Read(ref _intent); }
        private void RequireCurrent(int intent, CancellationToken cancellation)
        { cancellation.ThrowIfCancellationRequested(); if (!Current(intent)) throw new OperationCanceledException("The Proton integration change was cancelled."); }
        private void CancelOperation() { try { _operation?.Cancel(); } catch (ObjectDisposedException) { } }
        private void SetStatus(string value) { Status = value; StatusChanged?.Invoke(this, EventArgs.Empty); }
        private static string Friendly(Exception error)
        { return error is TimeoutException ? "The operation timed out." : error is OperationCanceledException ? "The operation was cancelled." : error.Message; }
        private async Task Bounded(Task task, CancellationToken cancellation) { await Deadline(Complete(task), cancellation); }
        private static async Task<bool> Complete(Task task) { await task; return true; }
        private async Task<T> Deadline<T>(Task<T> task, CancellationToken cancellation)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    if (await Task.WhenAny(task, Task.Delay(OperationTimeout, deadline.Token)) != task)
                    {
                        _ = task.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        cancellation.ThrowIfCancellationRequested(); throw new TimeoutException();
                    }
                    var result = await task; cancellation.ThrowIfCancellationRequested(); return result;
                }
                finally { deadline.Cancel(); }
            }
        }
        private async Task ReconcileLateRegistration(Task<CoreWebView2BrowserExtension> adding)
        {
            try
            {
                var extension = await adding; await _gate.WaitAsync();
                try
                {
                    if (!_desiredEnabled && extension.Id == ExtensionId)
                    {
                        await Bounded(extension.EnableAsync(false), CancellationToken.None);
                        if (_explicitRemoval) await Bounded(extension.RemoveAsync(), CancellationToken.None);
                        else if (!_disposed) File.WriteAllText(_registration, Path.GetFullPath(PackageFolder));
                    }
                }
                finally { _gate.Release(); }
            }
            catch { /* The profile may already have closed; no success is reported. */ }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _desiredEnabled = false; Interlocked.Increment(ref _intent);
            CancelOperation(); _lifetime.Cancel(); StatusChanged = null;
        }
    }
}
