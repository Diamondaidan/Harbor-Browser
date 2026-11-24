using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser.Models;

namespace HarborBrowser.Views
{
    public partial class ResourceMonitorWindow : Window
    {
        private readonly Func<string> _reportProvider;
        private readonly ResourceMonitorActions _actions;
        private readonly DispatcherTimer _timer;
        private readonly ObservableCollection<TabResourceRow> _rows=new ObservableCollection<TabResourceRow>();
        private bool _busy,_closed;
        public ResourceMonitorWindow(Func<string> reportProvider,ResourceMonitorActions actions=null)
        {
            InitializeComponent();HarborBrowser.Services.WindowFitService.Attach(this);_reportProvider=reportProvider;_actions=actions;
            TabGrid.ItemsSource=_rows;
            if(actions==null){TabsTab.Visibility=Visibility.Collapsed;MonitorTabs.SelectedItem=ProcessesTab;}
            _timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};_timer.Tick+=(s,e)=>RefreshReport();
            SizeChanged+=(s,e)=>UpdateCompact();Loaded+=(s,e)=>{UpdateCompact();RefreshReport();_timer.Start();};Closed+=(s,e)=>{_closed=true;_timer.Stop();};
        }
        private void UpdateCompact(){if(IntroHelp==null||TabHelp==null)return;bool compact=ActualHeight<430;IntroHelp.Visibility=compact?Visibility.Collapsed:Visibility.Visible;TabHelp.Visibility=compact?Visibility.Collapsed:Visibility.Visible;}
        private TabResourceRow Selected {get{return TabGrid.SelectedItem as TabResourceRow;}}
        private void Refresh_Click(object sender,RoutedEventArgs e){RefreshReport();}
        private void Close_Click(object sender,RoutedEventArgs e){Close();}
        private void TabGrid_SelectionChanged(object sender,SelectionChangedEventArgs e){UpdateActions();if(Selected!=null)TabGrid.ScrollIntoView(Selected);}
        private void UpdateActions()
        {
            if(ActivateTabButton==null)return;
            var row=Selected;ActivateTabButton.IsEnabled=!_busy&&row!=null;
            SleepTabButton.IsEnabled=!_busy&&row!=null&&row.CanSleep;WakeTabButton.IsEnabled=!_busy&&row!=null&&row.CanWake;
            UnloadTabButton.IsEnabled=!_busy&&row!=null&&row.CanUnload;
            WakeTabButton.Content=row!=null&&row.IsUnloaded?"Reload selected":"Wake selected";
            WakeTabButton.ToolTip=row!=null&&row.IsUnloaded?"Switches to this tab and reloads its saved address.":"Resumes the page without switching tabs.";
            KeepAwakeButton.IsEnabled=!_busy&&row!=null;KeepAwakeButton.Content=row!=null&&row.KeepAwake?"Allow sleep":"Keep awake";SleepInactiveButton.IsEnabled=!_busy&&_actions!=null;
        }
        private void ActivateTab_Click(object sender,RoutedEventArgs e){if(Selected!=null)_actions?.Activate?.Invoke(Selected.Id);RefreshReport();}
        private void WakeTab_Click(object sender,RoutedEventArgs e){if(Selected!=null)_actions?.Wake?.Invoke(Selected.Id);RefreshReport();}
        private void KeepAwake_Click(object sender,RoutedEventArgs e){if(Selected!=null)_actions?.KeepAwake?.Invoke(Selected.Id,!Selected.KeepAwake);RefreshReport();}
        private async void SleepTab_Click(object sender,RoutedEventArgs e){if(Selected!=null&&_actions?.Sleep!=null){Guid id=Selected.Id;await RunAction(()=>_actions.Sleep(id));}}
        private async void UnloadTab_Click(object sender,RoutedEventArgs e){if(Selected!=null&&_actions?.Unload!=null){Guid id=Selected.Id;await RunAction(()=>_actions.Unload(id));}}
        private async void SleepInactive_Click(object sender,RoutedEventArgs e){if(_actions?.SleepInactive!=null)await RunAction(_actions.SleepInactive);}
        private async Task RunAction(Func<Task> action)
        {
            if(_busy)return;_busy=true;UpdateActions();
            try{await action();}catch(Exception ex){if(!_closed)ReportBox.Text="Could not update tabs: "+ex.Message;}
            finally{_busy=false;if(!_closed)RefreshReport();}
        }
        private void RefreshReport()
        {
            if(_closed)return;
            try
            {
                ReportBox.Text=_reportProvider==null?"No resource data available.":_reportProvider();
                if(_actions?.Tabs!=null)
                {
                    var rows=_actions.Tabs();var selected=Selected;
                    foreach(var removed in _rows.Where(r=>!rows.Any(n=>n.Id==r.Id)).ToList())_rows.Remove(removed);
                    for(int i=0;i<rows.Count;i++)
                    {
                        var existing=_rows.FirstOrDefault(r=>r.Id==rows[i].Id);
                        if(existing==null)_rows.Insert(i,rows[i]);
                        else {existing.Update(rows[i]);int position=_rows.IndexOf(existing);if(position!=i)_rows.Move(position,i);}
                    }
                    if(selected!=null&&_rows.Contains(selected))TabGrid.SelectedItem=selected;else if(Selected==null)TabGrid.SelectedItem=_rows.FirstOrDefault();
                }
            }
            catch(Exception ex){ReportBox.Text="Unable to read resource information: "+ex.Message;}
            UpdateActions();
        }
    }
}
