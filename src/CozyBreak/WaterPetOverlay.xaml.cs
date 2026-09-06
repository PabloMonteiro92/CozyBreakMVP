using System.Windows;
using System.Media;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

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

    private void Drink_Click(object sender, RoutedEventArgs e) => Close();

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        _callbackAdiar(5);
        Close();
    }
}
