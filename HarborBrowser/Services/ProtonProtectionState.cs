using System;

namespace HarborBrowser.Services
{
    internal enum ProtonProtectionPhase { Off, Connecting, Protected, Disconnected }

    // A short-lived proof for hosted requests, not a system-wide VPN kill switch.
    internal sealed class ProtonProtectionState
    {
        private readonly object _gate = new object();
        private bool _enabled;
        private long _epoch;
        private ProtonProtectionPhase _state;
        private string _reason;
        private DateTime? _verifiedUntil, _lastVerified;
        internal Func<DateTime> UtcNow = () => DateTime.UtcNow;
        internal event EventHandler Changed;

        internal ProtonProtectionState(bool enabled)
        {
            _enabled = enabled;
            _state = enabled ? ProtonProtectionPhase.Connecting : ProtonProtectionPhase.Off;
            _reason = enabled ? "Browsing is paused until Proton's route is verified." : "Regular browsing is enabled.";
        }
        internal bool Enabled { get { lock (_gate) return _enabled; } }
        internal long Epoch { get { Expire(); lock (_gate) return _epoch; } }
        internal ProtonProtectionPhase State { get { Expire(); lock (_gate) return _state; } }
        internal string Reason { get { Expire(); lock (_gate) return _reason; } }
        internal DateTime? VerifiedUntilUtc { get { lock (_gate) return _verifiedUntil; } }
        internal DateTime? LastVerifiedUtc { get { lock (_gate) return _lastVerified; } }

        internal long Begin(bool enabled)
        {
            long epoch;
            lock (_gate)
            {
                _enabled = enabled; epoch = ++_epoch; _verifiedUntil = null;
                _state = enabled ? ProtonProtectionPhase.Connecting : ProtonProtectionPhase.Off;
                _reason = enabled ? "Browsing is paused until Proton's route is verified." : "Regular browsing is enabled.";
            }
            RaiseChanged(); return epoch;
        }
        internal long Invalidate(string reason, bool connecting = false)
        {
            long epoch;
            lock (_gate)
            {
                if (!_enabled) return _epoch;
                epoch = ++_epoch; _verifiedUntil = null;
                _state = connecting ? ProtonProtectionPhase.Connecting : ProtonProtectionPhase.Disconnected;
                _reason = String.IsNullOrWhiteSpace(reason) ? "Browsing is paused. Reconnect and verify Proton." : reason;
            }
            RaiseChanged(); return epoch;
        }
        internal bool TryProtect(long epoch, TimeSpan lease)
        {
            Expire();
            if (lease <= TimeSpan.Zero || lease > TimeSpan.FromMinutes(5)) return false;
            lock (_gate)
            {
                if (!_enabled || epoch != _epoch) return false;
                _lastVerified = UtcNow(); _verifiedUntil = _lastVerified.Value.Add(lease);
                _state = ProtonProtectionPhase.Protected;
                _reason = "Proton's browser route was verified. Hosted requests also check proxy ownership.";
            }
            RaiseChanged(); return true;
        }
        internal bool ShouldBlock(string target, bool trustedAuthentication)
        {
            if (!IsOrdinaryRequest(target, trustedAuthentication)) return false;
            return Enabled && State != ProtonProtectionPhase.Protected;
        }
        internal static bool IsOrdinaryRequest(string target, bool trustedAuthentication)
        {
            Uri uri;
            if (!Uri.TryCreate(target, UriKind.Absolute, out uri)) return false;
            if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "ws" && uri.Scheme != "wss") return false;
            if ((uri.Scheme == "https" || uri.Scheme == "http") && uri.Host == HomePageService.Host && uri.UserInfo.Length == 0 && uri.IsDefaultPort) return false;
            if (trustedAuthentication && IsAuthenticationOrigin(uri)) return false;
            return true;
        }
        internal static bool IsAuthenticationOrigin(Uri uri)
        {
            return uri != null && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
                (uri.Host == "account.proton.me" || uri.Host == "account.protonvpn.com" || uri.Host == "account-api.proton.me");
        }
        internal static bool IsProviderApiRequest(string target)
        {
            Uri uri;
            return Uri.TryCreate(target, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
                uri.Host == "account.proton.me" && uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal);
        }
        private void Expire()
        {
            bool changed = false;
            lock (_gate)
            {
                if (_enabled && _state == ProtonProtectionPhase.Protected && (!_verifiedUntil.HasValue || UtcNow() >= _verifiedUntil.Value))
                {
                    ++_epoch; _verifiedUntil = null; _state = ProtonProtectionPhase.Disconnected;
                    _reason = "The route check expired. Browsing is paused until Proton is verified again.";
                    changed = true;
                }
            }
            if (changed) RaiseChanged();
        }
        private void RaiseChanged() { Changed?.Invoke(this, EventArgs.Empty); }
    }
}
