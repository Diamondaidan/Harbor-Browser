using System;
using System.Diagnostics;
using HarborBrowser.Services;

internal static partial class Program
{
    // Synthetic clock changes never modify the Windows clock.
    private static void ProtectionClock061Checks()
    {
        var now=new DateTime(2026,11,17,12,0,0,DateTimeKind.Utc);
        long ticks=0;
        var state=new ProtonProtectionState(true){UtcNow=()=>now,MonotonicTimestamp=()=>ticks};
        long epoch=state.Begin(true);
        Check("clock safety accepts a current 30-second proof",state.TryProtect(epoch,TimeSpan.FromSeconds(30)));
        now=now.AddMinutes(-5);
        Check("backward clock change immediately closes the ordinary request gate",state.ShouldBlock("https://ordinary.invalid/",false)&&state.State==ProtonProtectionPhase.Disconnected);
        Check("clock rollback invalidates late pre-change verification",!state.TryProtect(epoch,TimeSpan.FromSeconds(30)));
        epoch=state.Epoch;
        Check("fresh proof can recover after a clock change",state.TryProtect(epoch,TimeSpan.FromSeconds(30)));
        ticks+=Stopwatch.Frequency*29;
        Check("monotonic proof remains valid before its deadline",!state.ShouldBlock("https://ordinary.invalid/",false));
        ticks+=Stopwatch.Frequency;
        Check("elapsed-time expiry closes the gate even when wall time stays fixed",state.ShouldBlock("https://ordinary.invalid/",false));
        Check("monotonic expiry rejects a late verification result",!state.TryProtect(epoch,TimeSpan.FromSeconds(30)));
        epoch=state.Epoch;state.TryProtect(epoch,TimeSpan.FromSeconds(30));
        now=now.AddDays(1);
        Check("forward clock jump also expires the proof",state.State==ProtonProtectionPhase.Disconnected);
        epoch=state.Epoch;state.TryProtect(epoch,TimeSpan.FromSeconds(30));
        ticks-=Stopwatch.Frequency;
        Check("an invalid elapsed-time source cannot preserve protection",state.State==ProtonProtectionPhase.Disconnected);
        epoch=state.Epoch;
        Check("zero and oversized verification leases remain rejected",!state.TryProtect(epoch,TimeSpan.Zero)&&!state.TryProtect(epoch,TimeSpan.FromMinutes(6)));
        epoch=state.Begin(false);now=now.AddDays(-2);ticks+=Stopwatch.Frequency*60;
        Check("clock changes do not arm protection when it is off",state.State==ProtonProtectionPhase.Off&&!state.ShouldBlock("https://ordinary.invalid/",false));
    }
}
