using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarborBrowser.Services;
using Microsoft.Win32;

namespace HarborBrowser.Views
{
    public sealed class SiteModulesWindow:Window
    {
        public readonly ListBox ModuleList=new ListBox{DisplayMemberPath="Display",MinHeight=65};
        public readonly TextBlock StatusText=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
        private readonly TextBlock _details=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
        private readonly WrapPanel _buttons=new WrapPanel();
        private readonly Button _toggle,_remove,_access;
        private bool _busy;
        public SiteModulesWindow(Func<Task<List<SiteModuleRow>>> read,Func<SiteModulePackage,Task<string>> install,Func<string,bool,bool,Task> change,Func<string,IEnumerable<string>,Task> sites=null)
        {
            Title="Site modules";Width=700;Height=600;MinWidth=360;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(this);
            var root=new Grid{Margin=new Thickness(14)};Content=root;
            root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);
            var body=new StackPanel();scroll.Content=body;
            body.Children.Add(new TextBlock{Text="Site modules",FontSize=23,FontWeight=FontWeights.SemiBold});
            body.Children.Add(new TextBlock{Text="Trusted local extensions can read and change the sites you allow. They run in normal tabs only. Changing access, enabling, disabling or removing reloads normal pages; save unfinished work first.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)});
            ModuleList.Height=150;body.Children.Add(ModuleList);body.Children.Add(_details);
            var footer=new StackPanel();Grid.SetRow(footer,1);root.Children.Add(footer);footer.Children.Add(_buttons);footer.Children.Add(StatusText);
            Func<string,Task> refresh=async selected=>
            {
                var rows=await read();ModuleList.ItemsSource=rows;ModuleList.SelectedItem=rows.FirstOrDefault(r=>r.Id==selected);
                _details.Text=rows.Count==0?"No local modules installed. Add a trusted manifest.json to begin.":"Select a module to review its access and status.";
                if(ModuleList.SelectedItem is SiteModuleRow)ShowDetails();StatusText.Text=rows.Count+" local module"+(rows.Count==1?"":"s");
            };
            Func<Func<Task>,Task> run=async action=>
            {
                _busy=true;_buttons.IsEnabled=false;ModuleList.IsEnabled=false;
                try{await action();}catch(Exception ex){StatusText.Text="Could not complete the action. "+ex.Message;}
                finally{_busy=false;_buttons.IsEnabled=true;ModuleList.IsEnabled=true;UpdateButtons(sites!=null);}
            };
            AddButton("Add module…",async()=>await run(async()=>
            {
                var picker=new OpenFileDialog{Filter="Module manifest|manifest.json",Title="Choose a trusted module's manifest.json"};if(picker.ShowDialog(this)!=true)return;
                var package=SiteModuleService.Inspect(Path.GetDirectoryName(picker.FileName));if(!Review(package))return;string id=await install(package);await refresh(id);
            }));
            _toggle=AddButton("Enable",async()=>await run(async()=>{var row=ModuleList.SelectedItem as SiteModuleRow;if(row==null)return;await change(row.Id,!row.Enabled,false);await refresh(row.Id);}));
            _access=AddButton("Site access…",async()=>await run(async()=>
            {
                var row=ModuleList.SelectedItem as SiteModuleRow;if(row==null||sites==null)return;
                var dialog=new ModuleSiteAccessWindow(row){Owner=this};if(dialog.ShowDialog()!=true)return;await sites(row.Id,dialog.AllowedSites);await refresh(row.Id);
            }));
            _remove=AddButton("Remove",async()=>await run(async()=>{var row=ModuleList.SelectedItem as SiteModuleRow;if(row==null)return;await change(row.Id,false,true);await refresh(null);}));
            AddButton("Refresh",async()=>await run(()=>refresh((ModuleList.SelectedItem as SiteModuleRow)?.Id)));
            ModuleList.SelectionChanged+=(s,e)=>{ShowDetails();UpdateButtons(sites!=null);};
            UpdateButtons(sites!=null);Loaded+=async(s,e)=>await run(()=>refresh(null));
        }
        private void ShowDetails(){var row=ModuleList.SelectedItem as SiteModuleRow;if(row!=null)_details.Text=row.AccessDetails;}
        private void UpdateButtons(bool sites)
        {
            var row=ModuleList.SelectedItem as SiteModuleRow;
            if(_toggle==null)return;_toggle.Content=row!=null&&row.Enabled?"Disable":"Enable";
            _toggle.IsEnabled=!_busy&&row!=null&&(row.Enabled||row.AllowedSites.Count>0)&&!row.Status.StartsWith("Blocked:",StringComparison.Ordinal);
            _remove.IsEnabled=!_busy&&row!=null;_access.IsEnabled=!_busy&&sites&&row!=null&&row.RequestedSites.Count>0;
        }
        private Button AddButton(string text,Func<Task> action){var button=new Button{Content=text,Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,5,6,0)};button.Click+=async(s,e)=>await action();_buttons.Children.Add(button);return button;}
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
    public sealed class ModuleSiteAccessWindow:Window
    {
        public readonly List<CheckBox> SiteChoices=new List<CheckBox>();
        public List<string> AllowedSites {get;private set;}
        public ModuleSiteAccessWindow(SiteModuleRow module)
        {
            Title="Site access — "+module.Name;Width=580;Height=440;MinWidth=340;MinHeight=280;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(this);
            var root=new DockPanel{Margin=new Thickness(14)};Content=root;
            var save=new Button{Content="Save site access",HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(save,Dock.Bottom);root.Children.Add(save);
            var body=new StackPanel();root.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=body});
            body.Children.Add(new TextBlock{Text="Choose the requested site patterns this module may access. A wildcard can cover many websites. If patterns overlap, another checked pattern may still allow the same page. Uncheck every pattern to stop all site access.",TextWrapping=TextWrapping.Wrap});
            foreach(string site in module.RequestedSites)
            {var choice=new CheckBox{Content=site,Tag=site,IsChecked=module.AllowedSites.Contains(site),Margin=new Thickness(0,10,0,0)};SiteChoices.Add(choice);body.Children.Add(choice);}
            body.Children.Add(new TextBlock{Text="Harbor Home and private tabs remain excluded. Normal pages reload after saving. A disabled module stays disabled until you enable it. Saving site access may reset this module\u2019s saved preferences.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)});
            save.Click+=(s,e)=>{AllowedSites=SiteChoices.Where(c=>c.IsChecked==true).Select(c=>(string)c.Tag).ToList();DialogResult=true;};
        }
    }
}
