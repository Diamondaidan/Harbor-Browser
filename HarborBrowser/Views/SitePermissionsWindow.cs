using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class PermissionRow
    {
        public string Origin {get;set;}
        public string TopLevelOrigin {get;set;}
        public string Kind {get;set;}
        public string Label {get{return SitePermissionService.Label(Kind);}}
        public string Decision {get;set;}
        public DateTime UpdatedUtc {get;set;}
        public string OriginalDecision {get;set;}
    }
    public sealed class SitePermissionsWindow : Window
    {
        public readonly DataGrid PermissionGrid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single,HeadersVisibility=DataGridHeadersVisibility.Column};
        public readonly TextBox SiteBox=new TextBox{Width=260};
        public List<SitePermissionRule> ResultRules {get;private set;}
        private readonly List<PermissionRow> rows=new List<PermissionRow>();
        public SitePermissionsWindow(IEnumerable<SitePermissionRule> rules,string current,bool isPrivate)
        {
            SetResourceReference(StyleProperty,typeof(Window));
            Title="Site permissions";Width=920;Height=560;MinWidth=510;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new System.Windows.Media.FontFamily("Segoe UI");FontSize=13;WindowFitService.Attach(this);
            foreach(var rule in rules)rows.Add(new PermissionRow{Origin=rule.Origin,TopLevelOrigin=rule.TopLevelOrigin,Kind=rule.Kind,Decision=rule.Decision,OriginalDecision=rule.Decision,UpdatedUtc=rule.UpdatedUtc});
            var root=new DockPanel{Margin=new Thickness(16)};Content=root;
            var footer=new DockPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);
            var cancel=new Button{Content="Cancel",IsCancel=true,Width=85};actions.Children.Add(cancel);var apply=new Button{Content="Apply and reload sites",IsDefault=true,Padding=new Thickness(12,5,12,5)};actions.Children.Add(apply);
            footer.Children.Add(new TextBlock{Text="Matching open pages reload to stop existing access.",TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)});
            var header=new StackPanel{Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            header.Children.Add(new TextBlock{Text="Site permissions",FontSize=23,FontWeight=FontWeights.SemiBold});
            header.Children.Add(new TextBlock{Text=isPrivate?"Private session — choices disappear when the session ends.":"Normal browsing — remembered choices are saved on this computer.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,8)});
            var bar=new WrapPanel();bar.Children.Add(new TextBlock{Text="Website",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});bar.Children.Add(SiteBox);var add=new Button{Content="Add site"};bar.Children.Add(add);var reset=new Button{Content="Reset selected to Ask"};bar.Children.Add(reset);var all=new Button{Content="Reset all to Ask"};bar.Children.Add(all);header.Children.Add(bar);
            PermissionGrid.Columns.Add(new DataGridTextColumn{Header="Requesting site",Binding=new Binding("Origin"),IsReadOnly=true,Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
            PermissionGrid.Columns.Add(new DataGridTextColumn{Header="Opened in",Binding=new Binding("TopLevelOrigin"),IsReadOnly=true,Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
            PermissionGrid.Columns.Add(new DataGridTextColumn{Header="Permission",Binding=new Binding("Label"),IsReadOnly=true,Width=130});
            PermissionGrid.Columns.Add(new DataGridComboBoxColumn{Header="Setting",ItemsSource=new[]{"Ask","Allow","Block"},SelectedItemBinding=new Binding("Decision"){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=110});
            root.Children.Add(PermissionGrid);
            add.Click+=(s,e)=>{try{AddSite(SiteBox.Text);}catch(ArgumentException ex){MessageBox.Show(this,ex.Message,Title);}};
            reset.Click+=(s,e)=>{CommitEdits();var selected=PermissionGrid.SelectedItem as PermissionRow;if(selected!=null){selected.Decision="Ask";PermissionGrid.Items.Refresh();}};
            all.Click+=(s,e)=>{CommitEdits();foreach(var row in rows)row.Decision="Ask";PermissionGrid.Items.Refresh();};
            apply.Click+=(s,e)=>{PermissionGrid.CommitEdit(DataGridEditingUnit.Cell,true);PermissionGrid.CommitEdit(DataGridEditingUnit.Row,true);var result=new SitePermissionService();foreach(var row in rows)result.Set(row.Origin,row.TopLevelOrigin,row.Kind,row.Decision,row.Decision==row.OriginalDecision?row.UpdatedUtc:DateTime.UtcNow);ResultRules=result.Snapshot();DialogResult=true;};
            if(SitePermissionService.Origin(current)!=null){SiteBox.Text=SitePermissionService.Origin(current);AddSite(current);}else Refresh();
        }
        internal void AddSite(string address)
        {
            string origin=SitePermissionService.Origin(address);if(origin==null)throw new ArgumentException("Enter a full HTTP or HTTPS website address.");
            foreach(string kind in new[]{"Camera","Microphone","Geolocation","Notifications"})
                if(!rows.Any(r=>r.Origin==origin&&r.TopLevelOrigin==origin&&r.Kind==kind))rows.Add(new PermissionRow{Origin=origin,TopLevelOrigin=origin,Kind=kind,Decision="Ask",OriginalDecision="Ask",UpdatedUtc=DateTime.UtcNow});
            Refresh();
        }
        private void Refresh(){PermissionGrid.ItemsSource=null;PermissionGrid.ItemsSource=rows;}
        private void CommitEdits(){PermissionGrid.CommitEdit(DataGridEditingUnit.Cell,true);PermissionGrid.CommitEdit(DataGridEditingUnit.Row,true);}
    }
}
