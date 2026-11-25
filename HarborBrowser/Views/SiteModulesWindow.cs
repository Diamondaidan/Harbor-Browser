using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Services;
using Microsoft.Win32;

namespace HarborBrowser.Views
{
    public sealed class SiteModulesWindow:Window
    {
        private readonly ListBox _list=new ListBox{DisplayMemberPath="Name",MinHeight=100};
        private readonly TextBlock _details=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)};
        private readonly TextBlock _status=new TextBlock{TextWrapping=TextWrapping.Wrap};
        private readonly WrapPanel _buttons=new WrapPanel();
        public SiteModulesWindow(Func<Task<List<SiteModuleRow>>> read,Func<SiteModulePackage,Task<string>> install,Func<string,bool,bool,Task> change)
        {
            Title="Site modules";Width=680;Height=580;MinWidth=380;MinHeight=330;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(this);
            var root=new DockPanel{Margin=new Thickness(16)};Content=root;
            var intro=new TextBlock{Text="Add trusted local modules for specific websites. This release supports Manifest V3 page scripts, styles and storage. Modules run in normal tabs only. Changes reload normal pages; save any unfinished work first.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(intro,Dock.Top);root.Children.Add(intro);
            var bottom=new StackPanel();bottom.Children.Add(_details);bottom.Children.Add(_buttons);bottom.Children.Add(_status);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);root.Children.Add(_list);
            Func<Task> refresh=async()=>{_list.ItemsSource=await read();_details.Text="Select a module to see its status and allowed sites.";_status.Text="";};
            Action<bool> busy=value=>{_buttons.IsEnabled=!value;_list.IsEnabled=!value;};
            Func<Func<Task>,Task> run=async action=>{busy(true);try{await action();}catch(Exception ex){_status.Text="Could not complete the module action. "+ex.Message;}finally{busy(false);}};
            AddButton("Add module…",async()=>await run(async()=>
            {
                var picker=new OpenFileDialog{Filter="Module manifest|manifest.json",Title="Choose a trusted module's manifest.json"};if(picker.ShowDialog(this)!=true)return;
                var package=SiteModuleService.Inspect(Path.GetDirectoryName(picker.FileName));
                if(!Review(package))return;await install(package);await refresh();
            }));
            AddButton("Enable / disable",async()=>await run(async()=>{var row=_list.SelectedItem as SiteModuleRow;if(row==null)return;await change(row.Id,!row.Enabled,false);await refresh();}));
            AddButton("Remove",async()=>await run(async()=>{var row=_list.SelectedItem as SiteModuleRow;if(row==null)return;await change(row.Id,false,true);await refresh();}));
            AddButton("Refresh",async()=>await run(refresh));
            _list.SelectionChanged+=(s,e)=>{var row=_list.SelectedItem as SiteModuleRow;if(row!=null)_details.Text=row.Name+" "+row.Version+"\n"+row.Status+"\nAllowed sites: "+row.Sites;};
            Loaded+=async(s,e)=>await run(refresh);
        }
        private void AddButton(string text,Func<Task> action){var button=new Button{Content=text,Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,0,6,10)};button.Click+=async(s,e)=>await action();_buttons.Children.Add(button);}
        private bool Review(SiteModulePackage package)
        {
            var dialog=new Window{Title="Review module access",Owner=this,Width=530,Height=450,MinWidth=340,MinHeight=300,WindowStartupLocation=WindowStartupLocation.CenterOwner};dialog.SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(dialog);
            var root=new DockPanel{Margin=new Thickness(16)};dialog.Content=root;
            var footer=new StackPanel();var grant=new CheckBox{Content="I trust this module and allow the access listed above.",Margin=new Thickness(0,10,0,10)};footer.Children.Add(grant);
            var install=new Button{Content="Install and reload normal tabs",IsEnabled=false,HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(10,6,10,6)};footer.Children.Add(install);
            grant.Checked+=(s,e)=>install.IsEnabled=true;grant.Unchecked+=(s,e)=>install.IsEnabled=false;install.Click+=(s,e)=>dialog.DialogResult=true;DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            root.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=package.Review,TextWrapping=TextWrapping.Wrap}});return dialog.ShowDialog()==true;
        }
    }
}
