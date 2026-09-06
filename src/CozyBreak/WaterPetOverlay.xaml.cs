using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CozyBreak;

public partial class WaterPetOverlay : Window
{
    // Sprites: "Tiny, Tiny Heroes - Animals" por Kacper Woźniak (thkaspar.itch.io/tth-animals), licença CC BY 4.0.
    private static readonly BitmapImage WalkSheet = new(new Uri("pack://application:,,,/Assets/Pet/mouse_walk.png"));
    private static readonly BitmapImage IdleSheet = new(new Uri("pack://application:,,,/Assets/Pet/mouse_idle.png"));

    private static readonly CroppedBitmap[] WalkFrames =
    [
        new CroppedBitmap(WalkSheet, new Int32Rect(0, 0, 16, 16)),
        new CroppedBitmap(WalkSheet, new Int32Rect(16, 0, 16, 16)),
        new CroppedBitmap(WalkSheet, new Int32Rect(32, 0, 16, 16)),
        new CroppedBitmap(WalkSheet, new Int32Rect(48, 0, 16, 16))
    ];

    private readonly Action<int> _callbackAdiar;
    private readonly DispatcherTimer _walkTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _frameIndex;

    public WaterPetOverlay(int doseMl, Action<int> callbackAdiar)
    {
        InitializeComponent();
        _callbackAdiar = callbackAdiar;
        TxtDose.Text = $"Hora da água! {doseMl} ml";
        _walkTimer.Tick += WalkTimer_Tick;
        Loaded += WaterPetOverlay_Loaded;
        Closed += (_, _) => _walkTimer.Stop();
    }

    private void WalkTimer_Tick(object? sender, EventArgs e)
    {
        PetImage.Source = WalkFrames[_frameIndex % WalkFrames.Length];
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
            PetImage.Source = IdleSheet;
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
