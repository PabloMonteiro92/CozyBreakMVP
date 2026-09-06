using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button; using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MediaBrush = System.Windows.Media.Brush;

namespace CozyBreak;

public partial class MainWindow : Window
{
    private readonly PerfilStore _store = new();
    private Perfil _perfil = new();
    private readonly DispatcherTimer _waterTimer = new();
    private readonly DispatcherTimer _breakTimer = new();
    private Forms.NotifyIcon? _tray;
    private WaterPetOverlay? _waterOverlay;
    private Window? _breakDialog;
    private DateTime _presentationUntil;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();
       System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        WaterSlider.ValueChanged += (_, _) => WaterValue.Text = $"{WaterSlider.Value:0} minutos";
        BreakSlider.ValueChanged += (_, _) => BreakValue.Text = $"{BreakSlider.Value:0} minutos";
        Loaded += (_, _) => LoadProfile();
        Closing += MainWindow_Closing;

        _waterTimer.Tick += WaterTimer_Tick;
        _breakTimer.Tick += BreakTimer_Tick;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Enquanto o app já está rodando em segundo plano (ícone na bandeja),
        // fechar esta janela (X) deve apenas escondê-la, nunca encerrar o processo.
        // Só encerra de fato quando "Encerrar" é escolhido no menu da bandeja.
        if (_tray is not null && !_exitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        Cleanup();
       System.Windows.Application.Current.Shutdown();
    }

    private void LoadProfile()
    {
        _perfil = _store.Carregar();
        NameBox.Text = _perfil.Usuario.Nome;
        AgeBox.Text = _perfil.Usuario.Idade > 0 ? _perfil.Usuario.Idade.ToString(CultureInfo.InvariantCulture) : string.Empty;
        WeightBox.Text = _perfil.Usuario.PesoKg > 0 ? _perfil.Usuario.PesoKg.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        HeightBox.Text = _perfil.Usuario.AlturaCm > 0 ? _perfil.Usuario.AlturaCm.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        WaterSlider.Value = _perfil.Configuracoes.IntervaloAguaMinutos;
        BreakSlider.Value = _perfil.Configuracoes.IntervaloPausaMinutos;
        SoundBox.IsChecked = _perfil.Configuracoes.SomHabilitado;

        CervicalBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Cervical);
        PunhosBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Punhos);
        LombarBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Lombar);
        JoelhosBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Joelhos);

        if (_store.Existe && Validacao.Perfil(_perfil) is null)
        {
            InitializeOrUpdateRuntime();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AgeBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var age) ||
            !Validacao.TentarConverterDouble(WeightBox.Text, out var weight) ||
            !Validacao.TentarConverterDouble(HeightBox.Text, out var height))
        {
            MessageBox.Show("Preencha idade, peso e altura com números válidos.", "Entrada Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _perfil.Usuario = new Usuario
        {
            Nome = NameBox.Text.Trim(),
            Idade = age,
            PesoKg = weight,
            AlturaCm = height,
            FocosDesconforto = SelectedFocos()
        };

        _perfil.Configuracoes = new Configuracoes
        {
            IntervaloAguaMinutos = (int)WaterSlider.Value,
            IntervaloPausaMinutos = (int)BreakSlider.Value,
            SomHabilitado = SoundBox.IsChecked == true,
            ModoApresentacaoAtivo = false
        };

        var erro = Validacao.Perfil(_perfil);
        if (erro is not null)
        {
            MessageBox.Show(erro, "Validação de Perfil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _store.Salvar(_perfil);
            InitializeOrUpdateRuntime();
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao persistir perfil: {ex.Message}", "Erro de Armazenamento", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<Foco> SelectedFocos()
    {
        var opcoes = new[]
        {
            (Selecionado: CervicalBox.IsChecked == true, Foco: Foco.Cervical),
            (Selecionado: PunhosBox.IsChecked == true, Foco: Foco.Punhos),
            (Selecionado: LombarBox.IsChecked == true, Foco: Foco.Lombar),
            (Selecionado: JoelhosBox.IsChecked == true, Foco: Foco.Joelhos)
        };

        return opcoes.Where(o => o.Selecionado).Select(o => o.Foco).ToList();
    }

    private void Presentation_Checked(object sender, RoutedEventArgs e)
    {
        _presentationUntil = DateTime.Now.AddHours(1);
        UpdatePresentationLabel(true);
    }

    private void Presentation_Unchecked(object sender, RoutedEventArgs e)
    {
        _presentationUntil = DateTime.MinValue;
        UpdatePresentationLabel(false);
    }

    private void UpdatePresentationLabel(bool enabled)
    {
        var state = enabled ? "ativado" : "desativado";
        PresentationBox.Content = $"Modo apresentação: {state}";
        PresentationBox.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Modo apresentação {state}");
    }

    private void InitializeOrUpdateRuntime()
    {
        if (_tray is null)
        {
            _tray = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true,
                Text = "CozyBreak"
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Configurações", null, (_, _) => { Show(); Activate(); });
            menu.Items.Add("Modo Apresentação (1h)", null, (_, _) => PresentationBox.IsChecked = PresentationBox.IsChecked != true);
            menu.Items.Add("Testar alerta de água", null, (_, _) =>
                Dispatcher.BeginInvoke(new Action(() => ShowWater(ignorePresentationMode: true))));
            menu.Items.Add("Pausa Imediata", null, (_, _) => ShowBreak());
            menu.Items.Add("-");
            menu.Items.Add("Encerrar", null, (_, _) => { _exitRequested = true; Close(); });
            _tray.ContextMenuStrip = menu;
        }

        _waterTimer.Stop();
        _waterTimer.Interval = TimeSpan.FromMinutes(_perfil.Configuracoes.IntervaloAguaMinutos);
        _waterTimer.Start();

        _breakTimer.Stop();
        _breakTimer.Interval = TimeSpan.FromMinutes(_perfil.IntervaloPausaEfetivoMinutos);
        _breakTimer.Start();
    }

    private void WaterTimer_Tick(object? sender, EventArgs e) => ShowWater();
    private void BreakTimer_Tick(object? sender, EventArgs e) => ShowBreak();

    private bool Pausado
    {
        get
        {
            if (PresentationBox.IsChecked == true && DateTime.Now >= _presentationUntil)
                PresentationBox.IsChecked = false;

            return PresentationBox.IsChecked == true;
        }
    }

    private void ShowWater(bool ignorePresentationMode = false)
    {
        if ((!ignorePresentationMode && Pausado) || _waterOverlay is not null) return;

        _waterOverlay = new WaterPetOverlay(_perfil.DoseAguaMl, minutosAdiar =>
        {
            _waterTimer.Stop();
            _waterTimer.Interval = TimeSpan.FromMinutes(minutosAdiar);
            _waterTimer.Start();
        }, _perfil.Configuracoes.SomHabilitado);
        _waterOverlay.Closed += (_, _) => _waterOverlay = null;
        _waterOverlay.Show();
    }

        private void ShowBreak()
    {
        if (Pausado || _breakDialog is not null) return;

        var exercicio = MotorExercicios.Escolher(_perfil.Usuario.FocosDesconforto);
        const double dialogWidth = 440;
        const double dialogHeight = 280;
        var telaAtiva = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;

        var dialog = new Window
        {
            Title = "Pausa Ativa Ergonômica",
            Width = dialogWidth,
            Height = dialogHeight,
            Left = telaAtiva.Left + (telaAtiva.Width - dialogWidth) / 2,
            Top = telaAtiva.Top + (telaAtiva.Height - dialogHeight) / 2,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Background = new SolidColorBrush(Color.FromRgb(255, 249, 243)),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(CreateAnimatedBear());
        panel.Children.Add(new TextBlock
        {
            Text = exercicio.Titulo,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(81, 66, 56))
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{exercicio.Instrucoes}\n\nTempo estimado: {exercicio.Segundos}s.\nSuspenda a execução caso sinta desconforto atípico.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 18),
            Foreground = new SolidColorBrush(Color.FromRgb(128, 111, 99))
        });

        var btnConcluir = new Button
        {
            Content = "Concluir Pausa",
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = new SolidColorBrush(Color.FromRgb(210, 139, 104)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
                btnConcluir.Click += (_, _) => dialog.Close();
        panel.Children.Add(btnConcluir);
        dialog.Content = panel;

        _breakDialog = dialog;
        dialog.Closed += (_, _) => _breakDialog = null;
        dialog.Show();
    }

    private static FrameworkElement CreateAnimatedBear()
    {
        var bear = new System.Windows.Controls.Canvas
        {
            Width = 72,
            Height = 58,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 4),
            RenderTransform = new TranslateTransform()
        };

        var fur = new SolidColorBrush(Color.FromRgb(166, 116, 78));
        var darkFur = new SolidColorBrush(Color.FromRgb(105, 67, 43));
        var muzzle = new SolidColorBrush(Color.FromRgb(224, 178, 133));
        AddPixel(bear, 8, 4, 14, 14, fur);
        AddPixel(bear, 50, 4, 14, 14, fur);
        AddPixel(bear, 14, 12, 44, 34, fur);
        AddPixel(bear, 24, 30, 24, 14, muzzle);
        AddPixel(bear, 25, 24, 5, 5, darkFur);
        AddPixel(bear, 42, 24, 5, 5, darkFur);
        AddPixel(bear, 32, 36, 8, 5, darkFur);

        var bob = new DoubleAnimation
        {
            From = 0,
            To = -4,
            Duration = TimeSpan.FromMilliseconds(650),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        ((TranslateTransform)bear.RenderTransform).BeginAnimation(TranslateTransform.YProperty, bob);
        return bear;
    }

    private static void AddPixel(System.Windows.Controls.Canvas canvas, double left, double top, double width, double height, MediaBrush fill)
    {
        var pixel = new System.Windows.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = fill,
            SnapsToDevicePixels = true
        };
        System.Windows.Controls.Canvas.SetLeft(pixel, left);
        System.Windows.Controls.Canvas.SetTop(pixel, top);
        canvas.Children.Add(pixel);
    }

    private void Cleanup()
    {
        _waterTimer.Stop();
        _breakTimer.Stop();

         _waterOverlay?.Close();
        _waterOverlay = null;

        _breakDialog?.Close();
        _breakDialog = null;

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _tray = null;
        }
    }
}
