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
        if (perfil.Usuario is null || perfil.Configuracoes is null || perfil.Usuario.FocosDesconforto is null)
            return "O perfil armazenado está incompleto.";

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
