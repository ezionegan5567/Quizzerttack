using System;
using System.Collections.Generic;
using Tienda;
using Xunit;

// LAB 11: la regla de trofeos, probada sin nube (dotnet test).
public class PruebasTrofeos
{
    // Al ganar: suman los trofeos del rival, ni uno más.
    [Fact]
    public void AlGanar_SumanLosTrofeosDelRival()
    {
        Assert.Equal(5, ReglasProgresion.CambioDeTrofeos(0, true, 5));
        Assert.Equal(25, ReglasProgresion.CambioDeTrofeos(25, true, 25));
    }

    // La regla de la guía: 5 trofeos y derrota contra la Guardia real (20)
    // -> mitad 10, pero solo hay 5 -> cambio -5 -> total 0 (NO -5).
    [Fact]
    public void AlPerder_RestaLaMitad_PeroNuncaMasDeLoQueHay()
    {
        var cambio = ReglasProgresion.CambioDeTrofeos(5, false, 20);
        Assert.Equal(-5, cambio);
        Assert.Equal(0, 5 + cambio);
    }

    // Sin trofeos, la derrota no resta nada.
    [Fact]
    public void AlPerder_ConCeroTrofeos_ElCambioEsCero()
    {
        Assert.Equal(0, ReglasProgresion.CambioDeTrofeos(0, false, 20));
    }

    // 10.000 combates al azar: el total NUNCA baja de cero y el cambio siempre es coherente.
    [Fact]
    public void MilDiezMilCombates_ElTotalNuncaBajaDeCero()
    {
        var azar = new Random(11_011);
        int total = 0;
        for (var i = 0; i < 10_000; i++)
        {
            bool victoria = azar.Next(0, 2) == 0;
            int trofeosRival = azar.Next(0, 41);          // 0..40
            int antes = total;
            var cambio = ReglasProgresion.CambioDeTrofeos(antes, victoria, trofeosRival);
            total = Math.Max(0, antes + cambio);

            Assert.True(total >= 0, $"El total quedó negativo en el combate {i}: {total}");
            if (victoria) Assert.Equal(antes + trofeosRival, total);
            else Assert.Equal(Math.Max(0, antes - trofeosRival / 2), total);
        }
    }

    // Tier por puesto: Oro 1-2, Plata 3-9, Bronce 10+ (Strategy = Rank de la tabla).
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "Oro")]
    [InlineData(2, "Oro")]
    [InlineData(3, "Plata")]
    [InlineData(9, "Plata")]
    [InlineData(10, "Bronce")]
    [InlineData(500, "Bronce")]
    public void TierPorPuesto_CoincideConLaTabla(int puesto, string esperado)
    {
        Assert.Equal(esperado, ReglasProgresion.TierPorPuesto(puesto));
    }

    // El premio de la temporada sale de Remote Config; si el tier no está, no hay premio.
    [Fact]
    public void PremioDe_SaleDeLaConfiguracion()
    {
        var cfg = new ConfigTemporada();
        Assert.Equal("Corona de oro", ReglasProgresion.PremioDe(cfg, "Oro").Nombre);
        Assert.Equal(20, ReglasProgresion.PremioDe(cfg, "Oro").Gemas);
        Assert.Equal("", ReglasProgresion.PremioDe(cfg, "").Nombre);
        Assert.Equal(0, ReglasProgresion.PremioDe(cfg, "Diamante").Gemas);
    }
}
