using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class ColorWheel : FrameworkElement
    {
        public static readonly DependencyProperty SelectedColorProperty=DependencyProperty.Register(
            "SelectedColor",typeof(Color),typeof(ColorWheel),new FrameworkPropertyMetadata(Colors.Coral,FrameworkPropertyMetadataOptions.AffectsRender,Changed));
        public Color SelectedColor {get{return (Color)GetValue(SelectedColorProperty);}set{SetValue(SelectedColorProperty,value);}}
        public event RoutedEventHandler SelectedColorChanged;
        private static readonly BitmapSource Wheel=MakeWheel();
        public ColorWheel(){Focusable=true;Cursor=Cursors.Cross;ToolTip="Choose hue and saturation. Arrow keys adjust color; use Brightness for light and dark shades.";MinWidth=160;MinHeight=160;}
        private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
        {
            var wheel=(ColorWheel)d;
            if(wheel.SelectedColorChanged!=null)wheel.SelectedColorChanged(wheel,new RoutedEventArgs());
        }
        private static BitmapSource MakeWheel()
        {
            const int n=200;byte[] pixels=new byte[n*n*4];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                double dx=(x-(n-1)/2.0)/(n/2.0-2),dy=(y-(n-1)/2.0)/(n/2.0-2),s=Math.Sqrt(dx*dx+dy*dy);
                if(s>1)continue;
                Color c=AppearanceService.FromHsv(Math.Atan2(-dy,dx)*180/Math.PI,s,1);
                int i=(y*n+x)*4;pixels[i]=c.B;pixels[i+1]=c.G;pixels[i+2]=c.R;pixels[i+3]=255;
            }
            var bitmap=BitmapSource.Create(n,n,96,96,PixelFormats.Bgra32,null,pixels,n*4);bitmap.Freeze();return bitmap;
        }
        protected override Size MeasureOverride(Size size){return new Size(180,180);}
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);double n=Math.Min(ActualWidth,ActualHeight),left=(ActualWidth-n)/2,top=(ActualHeight-n)/2;
            dc.DrawImage(Wheel,new Rect(left,top,n,n));
            double h,s,v;AppearanceService.ToHsv(SelectedColor,out h,out s,out v);
            double radius=n/2-2,angle=h*Math.PI/180;
            var point=new Point(ActualWidth/2+Math.Cos(angle)*s*radius,ActualHeight/2-Math.Sin(angle)*s*radius);
            dc.DrawEllipse(null,new Pen(Brushes.Black,3),point,6,6);dc.DrawEllipse(null,new Pen(Brushes.White,1.6),point,6,6);
            if(IsKeyboardFocused)dc.DrawEllipse(null,new Pen(Brushes.Gray,1),new Point(ActualWidth/2,ActualHeight/2),radius,radius);
        }
        private void SelectAt(Point p)
        {
            double radius=Math.Min(ActualWidth,ActualHeight)/2-2;if(radius<=0)return;
            double dx=(p.X-ActualWidth/2)/radius,dy=(ActualHeight/2-p.Y)/radius;
            double h,s,v;AppearanceService.ToHsv(SelectedColor,out h,out s,out v);
            SelectedColor=AppearanceService.FromHsv(Math.Atan2(dy,dx)*180/Math.PI,Math.Min(1,Math.Sqrt(dx*dx+dy*dy)),v<=0?1:v);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);Focus();CaptureMouse();SelectAt(e.GetPosition(this));e.Handled=true;}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)SelectAt(e.GetPosition(this));}
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(IsMouseCaptured)ReleaseMouseCapture();}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            double h,s,v;AppearanceService.ToHsv(SelectedColor,out h,out s,out v);
            if(e.Key==Key.Left)h-=5;else if(e.Key==Key.Right)h+=5;else if(e.Key==Key.Up)s+=.03;else if(e.Key==Key.Down)s-=.03;else return;
            SelectedColor=AppearanceService.FromHsv(h,s,v);e.Handled=true;
        }
    }
}
