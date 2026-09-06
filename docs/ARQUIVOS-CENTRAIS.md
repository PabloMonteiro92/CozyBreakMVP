# CozyBreak — arquivos centrais em texto legível

Este documento reúne os quatro arquivos principais do MVP em blocos de código Markdown para inspeção rápida.

## Domain.cs

```csharp
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
    private static readonly Exercicio[] Base =
    [
        new("Mobilidade global", "Role os ombros lentamente e olhe para um ponto distante por 20 segundos.", 60, null),
        new("Punhos e dedos", "Faça flexão e extensão suaves dos punhos; pare se houver dor ou formigamento.", 90, Foco.Punhos),
        new("Pescoço e trapézio", "Alongue suavemente sem forçar e mantenha os ombros relaxados.", 90, Foco.Cervical),
        new("Lombar e quadril", "Fique em pé, estenda o tronco confortavelmente e respire sem prender o ar.", 90, Foco.Lombar),
        new("Pernas e circulação", "Estenda os joelhos sentado e mova os tornozelos de forma confortável.", 90, Foco.Joelhos)
    ];

    public static Exercicio Escolher(IEnumerable<Foco> focos)
    {
        var focosSelecionados = focos.ToHashSet();
        return Base.FirstOrDefault(exercicio => exercicio.Foco is not null && focosSelecionados.Contains(exercicio.Foco.Value)) ?? Base[0];
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

        if (perfil.Usuario.PesoKg is < 25 or > 400 || double.IsNaN(perfil.Usuario.PesoKg))
            return "Informe um peso entre 25 e 400 kg.";

        if (perfil.Usuario.AlturaCm is < 100 or > 250 || double.IsNaN(perfil.Usuario.AlturaCm))
            return "Informe uma altura entre 100 e 250 cm.";

        if (perfil.Configuracoes.IntervaloAguaMinutos is < 30 or > 180)
            return "O intervalo de água deve estar entre 30 e 180 minutos.";

        if (perfil.Configuracoes.IntervaloPausaMinutos is < 20 or > 180)
            return "O intervalo de pausa deve estar entre 20 e 180 minutos.";

        return null;
    }
}
```

## Storage.cs

```csharp
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

            if (File.Exists(PathLegado))
            {
                var perfil = JsonSerializer.Deserialize<Perfil>(File.ReadAllText(PathLegado), Options) ?? new Perfil();
                if (Validacao.Perfil(perfil) is null)
                {
                    Salvar(perfil);
                    TryDelete(PathLegado);
                    return perfil;
                }
            }
        }
        catch (CryptographicException) { }
        catch (IOException) { }
        catch (JsonException) { }

        return new Perfil();
    }

    public void Salvar(Perfil perfil)
    {
        var erro = Validacao.Perfil(perfil);
        if (erro is not null) throw new ArgumentException(erro);

        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.SerializeToUtf8Bytes(perfil, Options);
        var protegido = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        var temporaryPath = PathProtegido + ".tmp";
        File.WriteAllBytes(temporaryPath, protegido);

        if (File.Exists(PathProtegido))
            File.Replace(temporaryPath, PathProtegido, null);
        else
            File.Move(temporaryPath, PathProtegido);
    }

    public void Apagar()
    {
        TryDelete(PathProtegido);
        TryDelete(PathLegado);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
    }
}
```

## MainWindow.xaml

```xml
<Window x:Class="CozyBreak.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="CozyBreak"
        Width="520"
        Height="650"
        WindowStartupLocation="CenterScreen"
        Background="#FFF9F3">
    <Grid Margin="28">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel AutomationProperties.Name="CozyBreak">
            <TextBlock Text="CozyBreak" FontSize="32" FontWeight="Bold" Foreground="#514238" />
            <TextBlock Text="Pausas gentis para um dia mais confortável" Foreground="#806F63" Margin="0,4,0,22" />
        </StackPanel>

        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <StackPanel>
                <TextBlock Text="Seu perfil" FontSize="20" FontWeight="SemiBold" Foreground="#514238" />
                <TextBlock Text="Nome ou apelido" Margin="0,16,0,4" />
                <TextBox x:Name="NameBox" AutomationProperties.Name="Nome ou apelido" Padding="8" />
                <TextBlock Text="Idade" Margin="0,12,0,4" />
                <TextBox x:Name="AgeBox" AutomationProperties.Name="Idade" Padding="8" />

                <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                    <StackPanel Width="210">
                        <TextBlock Text="Peso (kg)" Margin="0,0,0,4" />
                        <TextBox x:Name="WeightBox" AutomationProperties.Name="Peso em quilogramas" Padding="8" />
                    </StackPanel>
                    <StackPanel Width="210" Margin="16,0,0,0">
                        <TextBlock Text="Altura (cm)" Margin="0,0,0,4" />
                        <TextBox x:Name="HeightBox" AutomationProperties.Name="Altura em centímetros" Padding="8" />
                    </StackPanel>
                </StackPanel>

                <TextBlock Text="Onde sente desconforto? (opcional)" Margin="0,18,0,6" />
                <WrapPanel>
                    <CheckBox x:Name="CervicalBox" AutomationProperties.Name="Cervical ou trapézio" Content="Cervical / trapézio" Margin="0,4,14,4" />
                    <CheckBox x:Name="PunhosBox" AutomationProperties.Name="Punhos" Content="Punhos" Margin="0,4,14,4" />
                    <CheckBox x:Name="LombarBox" AutomationProperties.Name="Lombar ou quadril" Content="Lombar / quadril" Margin="0,4,14,4" />
                    <CheckBox x:Name="JoelhosBox" AutomationProperties.Name="Pernas" Content="Pernas" Margin="0,4,14,4" />
                </WrapPanel>

                <Separator Margin="0,22,0,18" />
                <TextBlock Text="Preferências" FontSize="20" FontWeight="SemiBold" Foreground="#514238" />
                <TextBlock Text="Lembrete de água (minutos)" Margin="0,14,0,4" />
                <Slider x:Name="WaterSlider" Minimum="30" Maximum="180" TickFrequency="15" IsSnapToTickEnabled="True" />
                <TextBlock x:Name="WaterValue" />
                <TextBlock Text="Lembrete de pausa (minutos)" Margin="0,12,0,4" />
                <Slider x:Name="BreakSlider" Minimum="20" Maximum="180" TickFrequency="10" IsSnapToTickEnabled="True" />
                <TextBlock x:Name="BreakValue" />
                <CheckBox x:Name="SoundBox" Content="Ativar som" Margin="0,14,0,4" />
                <TextBlock Text="A meta de água é uma estimativa de bem-estar, não uma orientação médica." TextWrapping="Wrap" Foreground="#806F63" Margin="0,18,0,0" />
            </StackPanel>
        </ScrollViewer>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,20,0,0">
            <Button x:Name="PresentationButton" AutomationProperties.Name="Ativar modo apresentação por uma hora" Content="Modo apresentação" Padding="12,8" Margin="0,0,10,0" Click="Presentation_Click" />
            <Button AutomationProperties.Name="Salvar perfil e começar" Content="Salvar e começar" Padding="16,8" Background="#D28B68" Foreground="White" Click="Save_Click" />
        </StackPanel>
    </Grid>
</Window>
```

## MainWindow.xaml.cs

```csharp
using System.Windows;
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
    private DateTime _presentationUntil;

    public MainWindow()
    {
        InitializeComponent();

        WaterSlider.ValueChanged += (_, _) => WaterValue.Text = $"{WaterSlider.Value:0} minutos";
        BreakSlider.ValueChanged += (_, _) => BreakValue.Text = $"{BreakSlider.Value:0} minutos";
        Loaded += (_, _) => LoadProfile();
        Closing += (_, _) => _tray?.Dispose();
    }

    private void LoadProfile()
    {
        _perfil = _store.Carregar();
        NameBox.Text = _perfil.Usuario.Nome;
        AgeBox.Text = _perfil.Usuario.Idade > 0 ? _perfil.Usuario.Idade.ToString() : string.Empty;
        WeightBox.Text = _perfil.Usuario.PesoKg > 0 ? _perfil.Usuario.PesoKg.ToString("0.##") : string.Empty;
        HeightBox.Text = _perfil.Usuario.AlturaCm > 0 ? _perfil.Usuario.AlturaCm.ToString("0.##") : string.Empty;
        WaterSlider.Value = _perfil.Configuracoes.IntervaloAguaMinutos;
        BreakSlider.Value = _perfil.Configuracoes.IntervaloPausaMinutos;
        SoundBox.IsChecked = _perfil.Configuracoes.SomHabilitado;

        CervicalBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Cervical);
        PunhosBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Punhos);
        LombarBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Lombar);
        JoelhosBox.IsChecked = _perfil.Usuario.FocosDesconforto.Contains(Foco.Joelhos);

        if (_store.Existe && Validacao.Perfil(_perfil) is null)
        {
            StartTrayAndTimers();
            Hide();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AgeBox.Text, out var age) ||
            !double.TryParse(WeightBox.Text, out var weight) ||
            !double.TryParse(HeightBox.Text, out var height))
        {
            MessageBox.Show("Preencha idade, peso e altura com números válidos.", "Verifique os dados", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        var error = Validacao.Perfil(_perfil);
        if (error is not null)
        {
            MessageBox.Show(error, "Verifique os dados", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _store.Salvar(_perfil);
            StartTrayAndTimers();
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível salvar o perfil: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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

        return opcoes.Where(opcao => opcao.Selecionado).Select(opcao => opcao.Foco).ToList();
    }

    private void Presentation_Click(object sender, RoutedEventArgs e)
    {
        _presentationUntil = DateTime.Now.AddHours(1);
        MessageBox.Show("Alertas pausados por 1 hora.", "Modo apresentação");
    }

    private void StartTrayAndTimers()
    {
        _tray ??= new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            Text = "CozyBreak"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir painel", null, (_, _) => { Show(); Activate(); });
        menu.Items.Add("Modo apresentação (1h)", null, (_, _) => _presentationUntil = DateTime.Now.AddHours(1));
        menu.Items.Add("Forçar pausa agora", null, (_, _) => ShowBreak());
        menu.Items.Add("Sair", null, (_, _) => Application.Current.Shutdown());
        _tray.ContextMenuStrip = menu;

        _waterTimer.Interval = TimeSpan.FromMinutes(_perfil.Configuracoes.IntervaloAguaMinutos);
        _waterTimer.Tick += (_, _) => ShowWater();
        _waterTimer.Start();

        _breakTimer.Interval = TimeSpan.FromMinutes(_perfil.Configuracoes.IntervaloPausaMinutos);
        _breakTimer.Tick += (_, _) => ShowBreak();
        _breakTimer.Start();
    }

    private bool Paused => DateTime.Now < _presentationUntil;

    private void ShowWater()
    {
        if (Paused) return;

        _tray?.ShowBalloonTip(5000, "CozyBreak", $"Hora da água — uma sugestão de {_perfil.DoseAguaMl} ml.", Forms.ToolTipIcon.Info);
    }

    private void ShowBreak()
    {
        if (Paused) return;

        var exercicio = MotorExercicios.Escolher(_perfil.Usuario.FocosDesconforto);
        var dialog = new Window
        {
            Title = "Pausa confortável",
            Width = 420,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 249, 243))
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = exercicio.Titulo,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(81, 66, 56))
        });

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"{exercicio.Instrucoes}\n\nDuração sugerida: {exercicio.Segundos} segundos.\nPare se houver dor, tontura ou formigamento.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 16)
        });

        var close = new System.Windows.Controls.Button
        {
            Content = "Concluir pausa",
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        close.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Concluir pausa");
        close.Click += (_, _) => dialog.Close();
        panel.Children.Add(close);
        dialog.Content = panel;

        var screen = Forms.Screen.FromPoint(Forms.Control.MousePosition);
        dialog.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - dialog.Width) / 2;
        dialog.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - dialog.Height) / 2;
        dialog.Show();
    }
}
```
