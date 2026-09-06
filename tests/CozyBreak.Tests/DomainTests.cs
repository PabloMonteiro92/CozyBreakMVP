using Xunit;

namespace CozyBreak.Tests;

public class DomainTests
{
    [Fact]
    public void MetaAguaFicaLimitadaAIntervaloSeguroDeAplicacao()
    {
        var perfil = new Perfil { Usuario = new Usuario { Nome = "Ana", Idade = 30, PesoKg = 80, AlturaCm = 170 } };
        Assert.Equal(2800, perfil.MetaAguaMl);
        Assert.InRange(perfil.DoseAguaMl, 150, 250);
    }

    [Fact]
    public void ImcAltoReduzIntervaloDePausaEfetivo()
    {
        var perfil = new Perfil
        {
            Usuario = new Usuario { Nome = "Ana", Idade = 30, PesoKg = 100, AlturaCm = 170 },
            Configuracoes = new Configuracoes { IntervaloPausaMinutos = 60 }
        };

        Assert.Equal(34.6, perfil.Imc);
        Assert.Equal(45, perfil.IntervaloPausaEfetivoMinutos);
    }

    [Theory]
    [InlineData("", "Informe um nome")]
    [InlineData("Pessoa", "idade")]
    public void PerfilInvalidoRetornaMensagem(string nome, string trecho)
    {
        var perfil = new Perfil { Usuario = new Usuario { Nome = nome, Idade = 10, PesoKg = 70, AlturaCm = 170 } };
        Assert.Contains(trecho, Validacao.Perfil(perfil)!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MotorAlternaEntreFocosCompativeis()
    {
        var focos = new[] { Foco.Cervical, Foco.Punhos };
        var escolhas = Enumerable.Range(0, 4).Select(_ => MotorExercicios.Escolher(focos).Foco).ToList();

        Assert.Contains(Foco.Cervical, escolhas);
        Assert.Contains(Foco.Punhos, escolhas);
        Assert.Equal(2, escolhas.Distinct().Count());
    }

    [Fact]
    public void SemFocoEscolheMobilidadeGlobal()
    {
        var exercicio = MotorExercicios.Escolher([]);
        Assert.Null(exercicio.Foco);
    }

    [Theory]
    [InlineData("72.5", 72.5)]
    [InlineData("72,5", 72.5)]
    public void ConversaoDoubleAceitaPontoEVirgula(string entrada, double esperado)
    {
        Assert.True(Validacao.TentarConverterDouble(entrada, out var valor));
        Assert.Equal(esperado, valor);
    }
}
