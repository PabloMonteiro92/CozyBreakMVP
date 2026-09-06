# CozyBreak — arquivos centrais em texto legível

Este documento reúne os quatro arquivos principais do MVP em blocos de código Markdown para inspeção rápida.

## Domain.cs

```csharp
using System.Globalization;
using System.Text.Json.Serialization;

namespace CozyBreak;

public enum Foco
{
    Cervical,
    Punhos,
    Lombar,
    Joelhos
}

public sealed class Perfil
{
    public Usuario Usuario { get; set; } = new();
    public Configuracoes Configuracoes { get; set; } = new();

    [JsonIgnore]
    public int MetaAguaMl => Math.Clamp((int)Math.Round(Usuario.PesoKg * 35), 1000, 5000);

    [JsonIgnore]
    public int DoseAguaMl => Math.Clamp(MetaAguaMl / 12, 150, 250);

    [JsonIgnore]
    public double Imc => Usuario.AlturaCm > 0
        ? Math.Round(Usuario.PesoKg / Math.Pow(Usuario.AlturaCm / 100.0, 2), 2)
        : 0;

    [JsonIgnore]
    public int IntervaloPausaEfetivoMinutos =>
        (Imc >= 30.0 && Configuracoes.IntervaloPausaMinutos > 45)
            ? 45
            : Configuracoes.IntervaloPausaMinutos;
}

public sealed class Usuario
{
    public string Nome { get; set; } = string.Empty;
    public int Idade { get; set; }
    public double PesoKg { get; set; }
    public double AlturaCm { get; set; }
    public List<Foco> FocosDesconforto { get; set; } = [];
}

public sealed class Configuracoes
{
    public int IntervaloAguaMinutos { get; set; } = 75;
    public int IntervaloPausaMinutos { get; set; } = 50;
    public bool ModoApresentacaoAtivo { get; set; }
    public bool SomHabilitado { get; set; } = true;
}

public sealed record Exercicio(string Titulo, string Instrucoes, int Segundos, Foco? Foco);

public static class MotorExercicios
{
    private static int _indiceRotacao;
    private static readonly object SyncLock = new();

    private static readonly Exercicio[] Base =
    [
        new("Mobilidade global", "Role os ombros lentamente e foque o olhar em um ponto distante por 20 segundos.", 60, null),
        new("Punhos e dedos", "Flexione e estenda os punhos suavemente. Descontinue em caso de dormência.", 90, Foco.Punhos),
        new("Pescoço e trapézio", "Incline suavemente a cabeça lateralmente mantendo a cintura escapular relaxada.", 90, Foco.Cervical),
        new("Lombar e quadril", "Em pé, estenda o tronco posteriormente com as mãos na pelve. Respire normalmente.", 90, Foco.Lombar),
        new("Pernas e circulação", "Sentado, eleve os calcanhares e estenda os joelhos sequencialmente.", 90, Foco.Joelhos)
    ];

    public static Exercicio Escolher(IEnumerable<Foco> focos)
    {
        lock (SyncLock)
        {
            var focosSelecionados = focos.ToHashSet();
            var compativeis = Base
                .Where(e => e.Foco is not null && focosSelecionados.Contains(e.Foco.Value))
                .ToList();

            if (compativeis.Count == 0)
                return Base[0];

            var selecionado = compativeis[_indiceRotacao % compativeis.Count];
            _indiceRotacao = (_indiceRotacao + 1) % int.MaxValue;
            return selecionado;
        }
    }
}

public static class Validacao
{
    public static string? Perfil(Perfil perfil)
    {
        if (string.IsNullOrWhiteSpace(perfil.Usuario.Nome) || perfil.Usuario.Nome.Length > 80)
            return "Informe um nome ou apelido entre 1 e 80 caracteres.";

        if (perfil.Usuario.Idade is < 13 or > 120)
            return "A idade deve estar entre 13 e 120 anos.";

        if (perfil.Usuario.PesoKg is < 25 or > 400 || double.IsNaN(perfil.Usuario.PesoKg) || double.IsInfinity(perfil.Usuario.PesoKg))
            return "Informe um peso entre 25 e 400 kg.";

        if (perfil.Usuario.AlturaCm is < 100 or > 250 || double.IsNaN(perfil.Usuario.AlturaCm) || double.IsInfinity(perfil.Usuario.AlturaCm))
            return "Informe uma altura entre 100 e 250 cm.";

        if (perfil.Configuracoes.IntervaloAguaMinutos is < 30 or > 180)
            return "O intervalo de hidratação deve situar-se entre 30 e 180 minutos.";

        if (perfil.Configuracoes.IntervaloPausaMinutos is < 20 or > 180)
            return "O intervalo de descanso deve situar-se entre 20 e 180 minutos.";

        return null;
    }

    public static bool TentarConverterDouble(string entrada, out double valor)
    {
        var normalizada = entrada.Trim().Replace(',', '.');
        return double.TryParse(normalizada, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }
}
```

## Storage.cs

```csharp
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CozyBreak;

public sealed class PerfilStore
{
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CozyBreak");
    private string PathProtegido => System.IO.Path.Combine(_directory, "perfil.json.dpapi");
    private string PathLegado => System.IO.Path.Combine(_directory, "perfil.json");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CozyBreak.profile.v1");

    public bool Existe => File.Exists(PathProtegido) || File.Exists(PathLegado);

    public Perfil Carregar()
    {
        try
        {
            if (File.Exists(PathProtegido))
            {
                var protegido = File.ReadAllBytes(PathProtegido);
                var json = ProtectedData.Unprotect(protegido, Entropy, DataProtectionScope.CurrentUser);
                var perfil = JsonSerializer.Deserialize<Perfil>(json, Options) ?? new Perfil();
                return Validacao.Perfil(perfil) is null ? perfil : new Perfil();
            }

            // Migração única de versões anteriores que usavam JSON sem proteção.
            if (File.Exists(PathLegado))
            {
                var perfil = JsonSerializer.Deserialize<Perfil>(File.ReadAllText(PathLegado), Options) ?? new Perfil();
                if (Validacao.Perfil(perfil) is null) { Salvar(perfil); TryDelete(PathLegado); return perfil; }
            }
        }
        catch (CryptographicException) { }
        catch (IOException) { }
        catch (JsonException) { }
        return new Perfil();
    }

    public void Salvar(Perfil perfil)
    {
        var error = Validacao.Perfil(perfil);
        if (error is not null) throw new ArgumentException(error);
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.SerializeToUtf8Bytes(perfil, Options);
        var protegido = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        var temporary = PathProtegido + ".tmp";
        File.WriteAllBytes(temporary, protegido);
        if (File.Exists(PathProtegido)) File.Replace(temporary, PathProtegido, null);
        else File.Move(temporary, PathProtegido);
    }

    public void Apagar()
    {
        TryDelete(PathProtegido);
        TryDelete(PathLegado);
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }
}
```

## MainWindow.xaml

```xml
<Window x:Class="CozyBreak.MainWindow" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Title="CozyBreak" Width="520" Height="650" WindowStartupLocation="CenterScreen" Background="#FFF9F3">
  <Grid Margin="28">
    <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
    <StackPanel AutomationProperties.Name="CozyBreak">
      <TextBlock Text="CozyBreak" FontSize="32" FontWeight="Bold" Foreground="#514238"/>
      <TextBlock Text="Pausas gentis para um dia mais confortável" Foreground="#806F63" Margin="0,4,0,22"/>
    </StackPanel>
    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
      <StackPanel>
        <TextBlock Text="Seu perfil" FontSize="20" FontWeight="SemiBold" Foreground="#514238"/>
        <TextBlock Text="Nome ou apelido" Margin="0,16,0,4"/><TextBox x:Name="NameBox" AutomationProperties.Name="Nome ou apelido" Padding="8"/>
        <TextBlock Text="Idade" Margin="0,12,0,4"/><TextBox x:Name="AgeBox" AutomationProperties.Name="Idade" Padding="8"/>
        <StackPanel Orientation="Horizontal" Margin="0,12,0,0"><StackPanel Width="210"><TextBlock Text="Peso (kg)" Margin="0,0,0,4"/><TextBox x:Name="WeightBox" AutomationProperties.Name="Peso em quilogramas" Padding="8"/></StackPanel><StackPanel Width="210" Margin="16,0,0,0"><TextBlock Text="Altura (cm)" Margin="0,0,0,4"/><TextBox x:Name="HeightBox" AutomationProperties.Name="Altura em centímetros" Padding="8"/></StackPanel></StackPanel>
        <TextBlock Text="Onde sente desconforto? (opcional)" Margin="0,18,0,6"/>
        <WrapPanel><CheckBox x:Name="CervicalBox" AutomationProperties.Name="Cervical ou trapézio" Content="Cervical / trapézio" Margin="0,4,14,4"/><CheckBox x:Name="PunhosBox" AutomationProperties.Name="Punhos" Content="Punhos" Margin="0,4,14,4"/><CheckBox x:Name="LombarBox" AutomationProperties.Name="Lombar ou quadril" Content="Lombar / quadril" Margin="0,4,14,4"/><CheckBox x:Name="JoelhosBox" AutomationProperties.Name="Pernas" Content="Pernas" Margin="0,4,14,4"/></WrapPanel>
        <Separator Margin="0,22,0,18"/>
        <TextBlock Text="Preferências" FontSize="20" FontWeight="SemiBold" Foreground="#514238"/>
        <TextBlock Text="Lembrete de água (minutos)" Margin="0,14,0,4"/><Slider x:Name="WaterSlider" Minimum="30" Maximum="180" TickFrequency="15" IsSnapToTickEnabled="True"/><TextBlock x:Name="WaterValue"/>
        <TextBlock Text="Lembrete de pausa (minutos)" Margin="0,12,0,4"/><Slider x:Name="BreakSlider" Minimum="20" Maximum="180" TickFrequency="10" IsSnapToTickEnabled="True"/><TextBlock x:Name="BreakValue"/>
        <CheckBox x:Name="SoundBox" Content="Ativar som" Margin="0,14,0,4"/>
        <TextBlock Text="A meta de água é uma estimativa de bem-estar, não uma orientação médica." TextWrapping="Wrap" Foreground="#806F63" Margin="0,18,0,0"/>
      </StackPanel>
    </ScrollViewer>
    <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,20,0,0"><Button x:Name="PresentationButton" AutomationProperties.Name="Ativar modo apresentação por uma hora" Content="Modo apresentação" Padding="12,8" Margin="0,0,10,0" Click="Presentation_Click"/><Button AutomationProperties.Name="Salvar perfil e começar" Content="Salvar e começar" Padding="16,8" Background="#D28B68" Foreground="White" Click="Save_Click"/></StackPanel>
  </Grid>
</Window>
```

## MainWindow.xaml.cs

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button; using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

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
```
