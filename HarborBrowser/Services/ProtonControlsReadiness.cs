using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    // Rendering evidence only. This never reads account, extension or proxy state.
    internal static class ProtonControlsReadiness
    {
        private const string RenderProbe = @"
            const visible=e=>{if(!e)return false;const r=e.getBoundingClientRect(),s=getComputedStyle(e);return r.width>0&&r.height>0&&s.visibility!=='hidden'&&s.display!=='none'&&s.opacity!=='0';};
            const actionable=e=>visible(e)&&!e.disabled&&e.getAttribute('aria-disabled')!=='true'&&getComputedStyle(e).pointerEvents!=='none';
            const any=s=>Array.from(document.querySelectorAll(s)).some(actionable);
            return {ready:document.readyState,spinner:visible(document.querySelector('#spinner')),
                signInAction:visible(document.querySelector('#sign-in-view'))&&any('#sign-in-view .sign-in-button'),
                loggedAction:visible(document.querySelector('#logged-view'))&&any('#logged-view .quick-connect-button,#logged-view .disconnection-button,#logged-view .side-button,#logged-view .sign-out-button,#logged-view .switch-account-button')};
        ";

        internal const string ProbeScript = @"(()=>{
            if(location.protocol!=='chrome-extension:'||location.hostname!=='jplgfhpmjnbigmhklmmbgecoobifkmpa')return null;
        " + RenderProbe + "})()";

        // Explicit origin binding lets isolated HTML fixtures exercise the same DOM
        // predicate without weakening the production extension-only guard.
        internal static string ProbeScriptForOrigin(string expectedOrigin)
        {
            string origin = SitePermissionService.Origin(expectedOrigin);
            if (origin == null || !String.Equals(origin, expectedOrigin, StringComparison.Ordinal))
                throw new ArgumentException("An exact HTTP(S) origin is required.", nameof(expectedOrigin));
            return "(()=>{if(location.origin!==" + new JavaScriptSerializer().Serialize(origin) + ")return null;" + RenderProbe + "})()";
        }

        internal static bool IsReady(string summary)
        {
            if (String.IsNullOrEmpty(summary) || summary.Length > 4096) return false;
            try
            {
                var state = new JavaScriptSerializer { MaxJsonLength = 4096, RecursionLimit = 4 }
                    .DeserializeObject(summary) as Dictionary<string, object>;
                object ready, spinner, signIn, logged;
                return state != null && state.TryGetValue("ready", out ready) &&
                    (Equals(ready, "complete") || Equals(ready, "interactive")) &&
                    state.TryGetValue("spinner", out spinner) && spinner is bool && !(bool)spinner &&
                    state.TryGetValue("signInAction", out signIn) && signIn is bool &&
                    state.TryGetValue("loggedAction", out logged) && logged is bool &&
                    ((bool)signIn || (bool)logged);
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
