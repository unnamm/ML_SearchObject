using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace UI.ComponentView;

public class ImageViewer : Border
{
    private Image _image;

    public static readonly DependencyProperty ImageSourceProperty =
           DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(ImageViewer),
               new FrameworkPropertyMetadata(null, OnImageSourceChanged));

    public static readonly DependencyProperty StretchProperty =
           DependencyProperty.Register("Stretch", typeof(Stretch), typeof(ImageViewer),
               new FrameworkPropertyMetadata(Stretch.Uniform, OnStretchChanged));

    public ImageSource ImageSource
    {
        get { return (ImageSource)GetValue(ImageSourceProperty); }
        set { SetValue(ImageSourceProperty, value); }
    }

    public Stretch Stretch
    {
        get { return (Stretch)GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    private UIElement child = null;
    private Point origin;
    private Point start;

    static ImageViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewer),
            new FrameworkPropertyMetadata(typeof(ImageViewer)));
    }

    public ImageViewer()
    {
        this.ClipToBounds = true;  // 자동으로 ClipToBounds 적용

        // 내부 Image 요소 생성
        _image = new Image();
        _image.Stretch = this.Stretch;  // 초기 Stretch 설정

        Initialize(_image);
        base.Child = _image;
    }

    private TranslateTransform GetTranslateTransform(UIElement element)
    {
        return (TranslateTransform)((TransformGroup)element.RenderTransform)
          .Children.First(tr => tr is TranslateTransform);
    }

    private ScaleTransform GetScaleTransform(UIElement element)
    {
        return (ScaleTransform)((TransformGroup)element.RenderTransform)
          .Children.First(tr => tr is ScaleTransform);
    }

    public override UIElement Child
    {
        get { return base.Child; }
        set
        {
            if (value != _image)
            {
                throw new InvalidOperationException("ImageViewer의 Child 속성은 직접 설정할 수 없습니다.");
            }
            base.Child = value;
        }
    }

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var imageViewer = d as ImageViewer;
        if (imageViewer != null)
        {
            imageViewer.OnImageSourceChanged(e.NewValue as ImageSource);
        }
    }

    private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var imageViewer = d as ImageViewer;
        if (imageViewer != null && imageViewer._image != null)
        {
            imageViewer._image.Stretch = (Stretch)e.NewValue;
        }
    }

    private void OnImageSourceChanged(ImageSource newValue)
    {
        if (_image != null)
        {
            _image.Source = newValue;
        }
    }

    /// <summary>
    /// 이미지 확대 적절하게 초기화
    /// </summary>
    public async void InitZoom()
    {
        // reset zoom
        var st = GetScaleTransform(child);
        st.ScaleX = 2.4;
        st.ScaleY = 2.4;

        while (ImageSource == null)
        {
            await Task.Delay(100);
        }
        // reset pan
        var tt = GetTranslateTransform(child);
        //tt.X = -640;
        tt.X = -ImageSource.Width / 2;
        //tt.Y = -480;
        tt.Y = -ImageSource.Height / 2;
    }

    private void Initialize(UIElement element)
    {
        this.child = element;
        if (child != null)
        {
            TransformGroup group = new TransformGroup();
            ScaleTransform st = new ScaleTransform();
            group.Children.Add(st);
            TranslateTransform tt = new TranslateTransform();
            group.Children.Add(tt);
            child.RenderTransform = group;
            child.RenderTransformOrigin = new Point(0.0, 0.0);
            this.MouseWheel += child_MouseWheel;
            this.MouseLeftButtonDown += child_MouseLeftButtonDown;
            this.MouseLeftButtonUp += child_MouseLeftButtonUp;
            this.MouseMove += child_MouseMove;
            this.PreviewMouseRightButtonDown += new MouseButtonEventHandler(
              child_PreviewMouseRightButtonDown);
            Reset();
        }
    }

    public void Reset()
    {
        if (child != null)
        {
            // reset zoom
            var st = GetScaleTransform(child);
            st.ScaleX = 1.0;
            st.ScaleY = 1.0;

            // reset pan
            var tt = GetTranslateTransform(child);
            tt.X = 0.0;
            tt.Y = 0.0;
        }
    }

    #region Child Events

    private void child_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (child != null)
        {
            var st = GetScaleTransform(child);
            var tt = GetTranslateTransform(child);

            double zoom = e.Delta > 0 ? .2 : -.2;
            if (!(e.Delta > 0) && (st.ScaleX < .4 || st.ScaleY < .4))
                return;

            Point relative = e.GetPosition(child);
            double absoluteX;
            double absoluteY;

            absoluteX = relative.X * st.ScaleX + tt.X;
            absoluteY = relative.Y * st.ScaleY + tt.Y;

            st.ScaleX += zoom;
            st.ScaleY += zoom;

            tt.X = absoluteX - relative.X * st.ScaleX;
            tt.Y = absoluteY - relative.Y * st.ScaleY;
        }
    }

    private void child_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (child != null)
        {
            var tt = GetTranslateTransform(child);
            start = e.GetPosition(this);
            origin = new Point(tt.X, tt.Y);
            this.Cursor = Cursors.Hand;
            child.CaptureMouse();
        }
    }

    private void child_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (child != null)
        {
            child.ReleaseMouseCapture();
            this.Cursor = Cursors.Arrow;
        }
    }

    void child_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        this.Reset();
    }

    private void child_MouseMove(object sender, MouseEventArgs e)
    {
        if (child != null)
        {
            if (child.IsMouseCaptured)
            {
                var tt = GetTranslateTransform(child);
                Vector v = start - e.GetPosition(this);
                tt.X = origin.X - v.X;
                tt.Y = origin.Y - v.Y;
            }
        }
    }

    #endregion
}