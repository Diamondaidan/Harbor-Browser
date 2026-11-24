using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task Downloads063Checks(MainWindow main,string output)
    {
        main.AutomaticProtonMonitoringEnabled=false;
        var original=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var downloads=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");
        var validator=main.ProtonProxyOwnershipValidator;
        using(var server=new DownloadFixtureServer(20))
        {
            original.View.CoreWebView2.Navigate(server.Url+"/resume063");
            await Wait(()=>downloads.Any(d=>d.IsActive&&d.BytesReceived>0));var entry=downloads.Single();
            main.DownloadAction(entry,"pause");await Wait(()=>entry.CanResume&&!entry.IsActive);
            Check("paused download explains session and possible restart",entry.RecoveryText.Contains("browser session")&&entry.RecoveryText.Contains("beginning"));
            var page=(DownloadsPage)main.OpenDownloadsPage().NativeContent;
            main.BeginProtonProtection(true);int requests=server.TransferRequests;
            main.DownloadAction(entry,"resume");await Task.Delay(100);
            Check("unverified protection blocks Resume without a new transfer",entry.Operation.State==CoreWebView2DownloadState.Interrupted&&server.TransferRequests==requests&&!entry.ResumePending);
            Check("blocked Resume becomes available for a later explicit retry",entry.CanRequestResume);
            main.MarkProtonRouteVerified(main.ProtonProtection.Epoch);
            var pending=new TaskCompletionSource<bool>();int validations=0;
            main.ProtonProxyOwnershipValidator=cancel=>{validations++;return pending.Task;};
            main.DownloadAction(entry,"resume");main.DownloadAction(entry,"resume");
            Check("double Resume queues one connection validation",validations==1&&entry.ResumePending&&!entry.CanRequestResume);
            Check("pending Resume does not start a network transfer",server.TransferRequests==requests);
            page.UpdateLayout();
            Check("pending Resume is disabled in the real Downloads UI",!Visuals<Button>(page).Single(b=>ReferenceEquals(b.DataContext,entry)&&Convert.ToString(b.Tag)=="resume").IsEnabled);
            pending.TrySetResult(true);await Wait(()=>entry.Status=="Complete");
            Check("explicit authorized Resume completes with exact original bytes",File.ReadAllBytes(entry.FilePath).SequenceEqual(server.Payload));
            Check("completed Resume retains one row and clears pending state",downloads.Count==1&&!entry.ResumePending&&!entry.CanRequestResume);
            main.BeginProtonProtection(false);main.ProtonProxyOwnershipValidator=validator;

            main.ConfirmPrivateDownload=()=>true;main.ConfirmPrivateDownloadClose=()=>true;
            var first=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=first.Item;await Wait(()=>first.Initialized);
            var create=typeof(MainWindow).GetMethod("CreateNewTab",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(string),typeof(bool),typeof(bool),typeof(BrowserSessionTab),typeof(bool),typeof(string),typeof(bool)},null);
            var second=(BrowserTab)create.Invoke(main,new object[]{HomePageService.Url,false,true,null,false,"p"+Guid.NewGuid().ToString("N"),false});main.BrowserTabs.SelectedItem=second.Item;await Wait(()=>second.Initialized);
            Check("private download owners have different native sessions",first.PrivateSessionId!=second.PrivateSessionId&&first.View.CoreWebView2.Profile.IsInPrivateModeEnabled&&second.View.CoreWebView2.Profile.IsInPrivateModeEnabled);
            first.View.CoreWebView2.Navigate(server.Url+"/private-resume063");second.View.CoreWebView2.Navigate(server.Url+"/private-resume063");
            await Wait(()=>downloads.Count(d=>d.IsPrivate&&d.IsActive&&d.BytesReceived>0)==2);
            var firstTransfer=downloads.Single(d=>d.PrivateSessionId==first.PrivateSessionId);var secondTransfer=downloads.Single(d=>d.PrivateSessionId==second.PrivateSessionId);
            main.DownloadAction(firstTransfer,"pause");main.DownloadAction(secondTransfer,"pause");await Wait(()=>firstTransfer.CanResume&&secondTransfer.CanResume&&!firstTransfer.IsActive&&!secondTransfer.IsActive);
            server.DisconnectTransfers();await Task.Delay(100);
            main.DownloadAction(firstTransfer,"resume");main.DownloadAction(secondTransfer,"resume");
            try{await Wait(()=>firstTransfer.Status=="Complete"&&secondTransfer.Status=="Complete");}
            finally{File.WriteAllText(Path.Combine(output,"private-resume-state.json"),Json.Serialize(downloads.Select(d=>new{d.Status,d.IsActive,d.CanResume,d.ResumePending,d.IsPrivate,FirstSession=d.PrivateSessionId==first.PrivateSessionId,SecondSession=d.PrivateSessionId==second.PrivateSessionId,OriginalRow=ReferenceEquals(d,firstTransfer)||ReferenceEquals(d,secondTransfer),NativeState=d.Operation?.State.ToString(),NativeReason=d.Operation?.InterruptReason.ToString(),Bytes=d.BytesReceived,FileBytes=File.Exists(d.FilePath)?new FileInfo(d.FilePath).Length:0}).ToArray()));}
            Check("same-URL resumes remain in their own private sessions",firstTransfer.PrivateSessionId==first.PrivateSessionId&&secondTransfer.PrivateSessionId==second.PrivateSessionId&&downloads.Count(d=>d.IsPrivate)==2);
            Check("private resumed files keep separate paths and exact bytes",firstTransfer.FilePath!=secondTransfer.FilePath&&File.ReadAllBytes(firstTransfer.FilePath).SequenceEqual(server.Payload)&&File.ReadAllBytes(secondTransfer.FilePath).SequenceEqual(server.Payload));
            CloseTab(main,first);CloseTab(main,second);await main.WaitForPrivateCleanup();
            Check("private cleanup removes rows while keeping explicit downloaded files",!downloads.Any(d=>d.IsPrivate)&&File.Exists(firstTransfer.FilePath)&&File.Exists(secondTransfer.FilePath));
            var displayed=main.OpenDownloadsPage();page=(DownloadsPage)displayed.NativeContent;
            var range=new DownloadEntry{FileName="Owned partial download.zip",Status="Interrupted: Server No Range",InterruptReason="ServerNoRange"};
            var signIn=new DownloadEntry{FileName="Owned expired download.zip",Status="Interrupted: Server Unauthorized",InterruptReason="ServerUnauthorized"};
            var disk=new DownloadEntry{FileName="Owned disk-full download.zip",Status="Interrupted: File No Space",InterruptReason="FileNoSpace"};
            downloads.Add(range);downloads.Add(signIn);downloads.Add(disk);
            Check("nonresumable transfer explains requesting a fresh download",range.RecoveryText.Contains("partial transfer")&&!range.CanRequestResume);
            Check("expired download points to website sign-in",signIn.RecoveryText.Contains("sign-in"));
            Check("disk-full failure explains freeing space",disk.RecoveryText.Contains("disk space"));
            foreach(string mode in new[]{"Dark","Light"})
            {
                AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});main.UpdateLayout();await Task.Delay(80);
                Check("download recovery text is visible in "+mode,Visuals<TextBlock>(page).Any(t=>t.Text==range.RecoveryText&&t.IsVisible));
                FindVisual<ScrollViewer>(page).ScrollToBottom();main.UpdateLayout();await Task.Delay(80);
                Capture(page,Path.Combine(output,"download-recovery-"+mode+".png"));
            }
            downloads.Remove(range);downloads.Remove(signIn);downloads.Remove(disk);
        }
        main.ProtonProxyOwnershipValidator=validator;main.BeginProtonProtection(false);
    }
}
