using System.Windows;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace CozyBreak;

public partial class WaterPetOverlay : Window
{
    private readonly Action<int> _callbackAdiar;

    public WaterPetOverlay(int doseMl, Action<int> callbackAdiar)
    {
        InitializeComponent();
        _callbackAdiar = callbackAdiar;
        TxtDose.Text = $"Hora da água! {doseMl} ml";
        Loaded += WaterPetOverlay_Loaded;
    }

    private void WaterPetOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        var tela = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Top = tela.Bottom - Height - 16;
        var startX = tela.Right;
        var endX = tela.Right - Width - 16;
        Left = startX;

        var anim = new DoubleAnimation
        {
            From = startX,
            To = endX,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
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
