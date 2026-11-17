using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public partial class SettingsWindow
    {
        private BrowserSettings _appearanceDraft;
        private bool _updatingAppearance;
        private string Mode { get { return Convert.ToString(AppearanceModeBox.SelectedValue)=="Dark"?"Dark":"Light"; } }
        private string Target { get { return Convert.ToString(ColorTargetBox.SelectedValue)??"Accent"; } }

        private void InitializeAppearance(BrowserSettings settings)
        {
            _appearanceDraft=settings.Copy();AppearanceService.Normalize(_appearanceDraft);
            _updatingAppearance=true;
            AppearanceModeBox.SelectedValue=_appearanceDraft.ThemeMode;
            ColorTargetBox.SelectedValue="Accent";
            _updatingAppearance=false;
            RefreshAppearance();
        }
        private void AppearanceMode_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(_appearanceDraft==null||_updatingAppearance)return;
            _appearanceDraft.ThemeMode=Mode;RefreshAppearance();
        }
        private void ColorTarget_Changed(object sender,SelectionChangedEventArgs e){if(_appearanceDraft!=null&&!_updatingAppearance)RefreshAppearance();}
        private void RefreshAppearance()
        {
            AppearanceService.Apply(Resources,_appearanceDraft);
            var palette=AppearanceService.Palette(_appearanceDraft);
            Color color=palette["Harbor"+(Target=="Background"?"Window":Target)];
            _updatingAppearance=true;
            AppearanceWheel.SelectedColor=color;
            AppearanceHexBox.Text=AppearanceService.Hex(color);
            AppearanceSwatch.Background=new SolidColorBrush(color);
            double h,s,v;AppearanceService.ToHsv(color,out h,out s,out v);BrightnessSlider.Value=v*100;
            _updatingAppearance=false;
            AppearanceWarning.Text=AppearanceService.Contrast(palette["HarborText"],palette["HarborWindow"])<4.5 ||
                AppearanceService.Contrast(palette["HarborText"],palette["HarborInput"])<4.5 ||
                AppearanceService.Contrast(palette["HarborText"],palette["HarborPanel"])<4.5
                ?"Some text may be hard to read. Increase the contrast between text and surfaces.":"";
        }
        private void SetColor(string value)
        {
            var colors=AppearanceService.Colors(_appearanceDraft,Mode);
            switch(Target){case "Background":colors.Background=value;break;case "Panel":colors.Panel=value;break;
                case "Input":colors.Input=value;break;case "Text":colors.Text=value;break;default:colors.Accent=value;break;}
            RefreshAppearance();
        }
        private void AppearanceWheel_Changed(object sender,RoutedEventArgs e)
        {
            if(_appearanceDraft!=null&&!_updatingAppearance)SetColor(AppearanceService.Hex(AppearanceWheel.SelectedColor));
        }
        private void Brightness_Changed(object sender,RoutedPropertyChangedEventArgs<double> e)
        {
            if(_appearanceDraft==null||_updatingAppearance)return;
            double h,s,v;AppearanceService.ToHsv(AppearanceWheel.SelectedColor,out h,out s,out v);
            SetColor(AppearanceService.Hex(AppearanceService.FromHsv(h,s,BrightnessSlider.Value/100)));
        }
        private void AppearanceHex_Changed(object sender,RoutedEventArgs e){CommitHex();}
        private bool CommitHex()
        {
            if(_appearanceDraft==null||_updatingAppearance)return true;
            Color color;
            if(!AppearanceService.TryColor(AppearanceHexBox.Text,out color))
            {
                AppearanceWarning.Text="Enter a hex color such as #A95627.";return false;
            }
            SetColor(AppearanceService.Hex(color));return true;
        }
        private void AppearanceHex_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){CommitHex();e.Handled=true;}}
        private void ResetColor_Click(object sender,RoutedEventArgs e){SetColor(null);}
        private void ResetAppearance_Click(object sender,RoutedEventArgs e)
        {
            if(Mode=="Dark")_appearanceDraft.DarkColors=new AppearanceColors();else _appearanceDraft.LightColors=new AppearanceColors();
            RefreshAppearance();
        }
    }
}
