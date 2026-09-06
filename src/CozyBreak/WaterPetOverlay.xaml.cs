using System.Windows;
using System.Media;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace CozyBreak;

public partial class WaterPetOverlay : Window
{
    private readonly Action<int> _callbackAdiar;
    private readonly DispatcherTimer _walkTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _frameIndex;

    public WaterPetOverlay(int doseMl, Action<int> callbackAdiar, bool soundEnabled)
    {
        InitializeComponent();
        _callbackAdiar = callbackAdiar;
        TxtDose.Text = $"Hora da água! {doseMl} ml";
        CreatePixelMouse();
        if (soundEnabled)
            SystemSounds.Asterisk.Play();
        _walkTimer.Tick += WalkTimer_Tick;
        Loaded += WaterPetOverlay_Loaded;
        Closed += (_, _) => _walkTimer.Stop();
    }

    private void WalkTimer_Tick(object? sender, EventArgs e)
    {
        PetCanvas.RenderTransform = new TranslateTransform(0, _frameIndex % 2 == 0 ? 0 : -2);
        _frameIndex++;
    }

    private void WaterPetOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        var tela = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Top = tela.Bottom - Height - 16;
        var startX = tela.Right;
        var endX = tela.Right - Width - 16;
        Left = startX;

        _walkTimer.Start();

        var anim = new DoubleAnimation
        {
            From = startX,
            To = endX,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Completed += (_, _) =>
        {
            _walkTimer.Stop();
            PetCanvas.RenderTransform = new TranslateTransform();
        };

        BeginAnimation(LeftProperty, anim);
    }

    private void CreatePixelMouse()
    {
        var fur = new SolidColorBrush(MediaColor.FromRgb(190, 130, 92));
        var darkFur = new SolidColorBrush(MediaColor.FromRgb(112, 70, 48));
        var ear = new SolidColorBrush(MediaColor.FromRgb(235, 163, 157));
        var eye = new SolidColorBrush(MediaColor.FromRgb(35, 25, 22));

        AddPixel(8, 22, 30, 18, fur);
        AddPixel(28, 14, 18, 20, fur);
        AddPixel(31, 9, 10, 10, ear);
        AddPixel(43, 18, 7, 7, darkFur);
        AddPixel(39, 16, 4, 4, eye);
        AddPixel(14, 40, 6, 11, darkFur);
        AddPixel(31, 40, 6, 11, darkFur);
        AddPixel(2, 22, 9, 5, fur);
    }

    private void AddPixel(double left, double top, double width, double height, MediaBrush fill)
    {
        PetCanvas.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = fill,
            SnapsToDevicePixels = true
        });
        System.Windows.Controls.Canvas.SetLeft(PetCanvas.Children[^1], left);
        System.Windows.Controls.Canvas.SetTop(PetCanvas.Children[^1], top);
    }

    private void Drink_Click(object sender, RoutedEventArgs e) => Close();

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        _callbackAdiar(5);
        Close();
    }
}
