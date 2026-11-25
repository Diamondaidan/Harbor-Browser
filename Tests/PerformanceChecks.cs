using System;
using HarborBrowser.Services;

internal static partial class Program
{
    private static void PerformanceChecks()
    {
        var now=DateTime.UtcNow;
        Check("sleep schedule uses original inactivity start",TabSleepSchedule.Delay(now.AddSeconds(-40),default(DateTime),now,60).TotalSeconds==20);
        Check("sleep schedule is due after inactivity delay",TabSleepSchedule.Delay(now.AddMinutes(-2),default(DateTime),now,60).TotalMilliseconds==100);
        Check("sleep retry does not create a busy loop",TabSleepSchedule.Delay(now.AddMinutes(-2),now.AddSeconds(30),now,60).TotalSeconds==30);
        Check("sleep delay respects supported lower bound",TabSleepSchedule.Delay(now,default(DateTime),now,0).TotalSeconds==15);
        Check("sleep delay respects supported upper bound",TabSleepSchedule.Delay(now,default(DateTime),now,900).TotalSeconds==600);
        var cpu=new ProcessCpuSampler();
        Check("first CPU observation is sampling",cpu.SamplePercent(42,100,TimeSpan.Zero,1000,1000,4)==null);
        Check("CPU sample normalizes total machine capacity",Math.Abs(cpu.SamplePercent(42,100,TimeSpan.FromSeconds(1),2000,1000,4).Value-25)<0.001);
        Check("PID reuse starts a fresh CPU sample",cpu.SamplePercent(42,200,TimeSpan.Zero,3000,1000,4)==null);
        Check("CPU counter rollback resets observation",cpu.SamplePercent(42,200,TimeSpan.FromSeconds(-1),4000,1000,4)==null);
        Check("non-increasing clock does not divide by zero",cpu.SamplePercent(42,200,TimeSpan.Zero,4000,1000,4)==null);
        cpu.Retain(new int[0]);Check("departed processes do not retain stale CPU samples",cpu.SamplePercent(42,200,TimeSpan.Zero,5000,1000,4)==null);
        Check("CPU output is bounded",cpu.SamplePercent(42,200,TimeSpan.FromSeconds(100),6000,1000,1)==100);
    }
}
