using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private bool _externalNavigationReady;
        private readonly Queue<string> _externalTargets=new Queue<string>();
        internal void OpenExternalTargets(IEnumerable<string> targets)
        {
            foreach(string raw in targets??Enumerable.Empty<string>())
            {
                string value=ExternalNavigationService.Target(raw);
                if(value==null&&Uri.TryCreate(raw,UriKind.Absolute,out var file)&&file.IsFile)value=ExternalNavigationService.Target(file.LocalPath);
                if(value!=null)_externalTargets.Enqueue(value);
            }
            FlushExternalTargets();
            if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
            Activate();
        }
        private void FlushExternalTargets()
        {
            if(!_externalNavigationReady)return;
            while(_externalTargets.Count>0)
            {
                // Windows links always enter normal browsing, even from a private tab.
                var tab=CreateNewTab(_externalTargets.Dequeue(),true,false,null);
                if(tab.Item.Visibility!=Visibility.Visible){RefreshGroupFilterBox("*");ApplyTabGroupFilter();BrowserTabs.SelectedItem=tab.Item;}
            }
        }
        private void DefaultBrowser_Click(object sender,RoutedEventArgs e)
        {
            try{ExternalNavigationService.OpenDefaultApps();SetStatus("Choose "+ProtonTrialIdentity.RegisteredName+" in Windows Default apps.");}
            catch(Exception ex){MessageBox.Show(this,"Open Windows Settings > Apps > Default apps and choose "+ProtonTrialIdentity.RegisteredName+". Install Harbor first if it is missing. "+ex.Message,"Default browser");}
        }
    }
}
