using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CozyBreak;

public partial class MainWindow : Window
{
    private readonly PerfilStore _store = new();
    private Perfil _perfil = new();
    private readonly DispatcherTimer _waterTimer = new();
    private readonly DispatcherTimer _breakTimer = new();
    private Forms.NotifyIcon? _tray;
    private WaterPetOverlay? _waterOverlay;
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
            Hide();
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

    private void Presentation_Click(object sender, RoutedEventArgs e)
    {
        _presentationUntil = DateTime.Now.AddHours(1);
        MessageBox.Show("Alertas suspensos durante a próxima hora.", "Modo Apresentação", MessageBoxButton.OK, MessageBoxImage.Information);
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
            menu.Items.Add("Modo Apresentação (1h)", null, (_, _) => _presentationUntil = DateTime.Now.AddHours(1));
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

    private bool Pausado => DateTime.Now < _presentationUntil;

    private void ShowWater()
    {
        if (Pausado || _waterOverlay is not null) return;

        _waterOverlay = new WaterPetOverlay(_perfil.DoseAguaMl, minutosAdiar =>
        {
            _waterTimer.Stop();
            _waterTimer.Interval = TimeSpan.FromMinutes(minutosAdiar);
            _waterTimer.Start();
        });
        _waterOverlay.Closed += (_, _) => _waterOverlay = null;
        _waterOverlay.Show();
    }

    private void ShowBreak()
    {
        if (Pausado) return;

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
        dialog.Show();
    }

    private void Cleanup()
    {
        _waterTimer.Stop();
        _breakTimer.Stop();

        _waterOverlay?.Close();
        _waterOverlay = null;

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _tray = null;
        }
    }
}
