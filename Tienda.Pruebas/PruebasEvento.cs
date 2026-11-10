using System;
using System.Collections.Generic;
using Tienda;
using Xunit;

// LAB 12: las reglas del evento, probadas sin nube (dotnet test).
// Todo lo que se prueba aquí es puro: la ventana en UTC, el factor de daño, el
// descuento del catálogo, el botín exclusivo y el bonus de bienvenida.
public class PruebasEvento
{
    // La ventana del laboratorio: 17:05 -> 17:12 UTC (7 minutos de evento).
    static readonly DateTime Inicio = new DateTime(2026, 11, 7, 17, 5, 0, DateTimeKind.Utc);
    static readonly DateTime Fin = new DateTime(2026, 11, 7, 17, 12, 0, DateTimeKind.Utc);

    static ConfigEvento Campana(bool activo = true)
    {
        return new ConfigEvento
        {
            Activo = activo,
            Id = "lab12-festival-de-la-nube",
            Nombre = "Lab12 - Festival de la Nube",
            InicioUtc = "2026-11-07T17:05:00Z",
            FinUtc = "2026-11-07T17:12:00Z",
            MultiplicadorDano = 1.25,
            DescuentoTienda = 0.3,
            HeroesEspeciales = new List<string> { "mago" },
            BotinExclusivo = new List<PremioBotin>
            {
                new PremioBotin { Id = "EMBLEMA_FESTIVAL", Tipo = "objeto", Cantidad = 1, Peso = 25 }
            }
        };
    }

    // 1) Sin evento, el combate es el del S10: factor 1.0 y ningún número cambia.
    [Fact]
    public void SinEvento_ElFactorEsUno_YNoHayVentana()
    {
        var apagado = new ConfigEvento();                       // la base del .rc
        Assert.False(ReglasEvento.Activo(apagado, Inicio));
        Assert.Equal(1.0, ReglasEvento.FactorDano(apagado, Inicio));
        Assert.Equal(0, ReglasEvento.SegundosRestantes(apagado, Inicio));
        Assert.Equal(12, ReglasEvento.AtaqueEscalado(12, 1.0)); // 12 se queda en 12
    }

    // 2) Con el evento activo, el daño escala el ataque ANTES de simular:
    //    12 de ataque x 1.25 = 15 (el redondeo lo hace el servidor).
    [Fact]
    public void ConEvento_ElAtaqueSeEscala()
    {
        var evento = Campana();
        var ahora = Inicio.AddMinutes(1);
        var factor = ReglasEvento.FactorDano(evento, ahora);

        Assert.Equal(1.25, factor);
        Assert.Equal(15, ReglasEvento.AtaqueEscalado(12, factor));
        Assert.Equal(25, ReglasEvento.AtaqueEscalado(20, factor));
        Assert.Equal(1, ReglasEvento.AtaqueEscalado(0, factor)); // nunca 0 de ataque
    }

    // 3) La ventana es UTC y es [inicio, fin): antes no existe, después tampoco.
    [Fact]
    public void LaVentanaEsUtc_YAbreYCierraSola()
    {
        var evento = Campana();

        Assert.False(ReglasEvento.Activo(evento, Inicio.AddSeconds(-1)));  // 17:04:59
        Assert.True(ReglasEvento.Activo(evento, Inicio));                  // 17:05:00
        Assert.True(ReglasEvento.Activo(evento, Fin.AddSeconds(-1)));      // 17:11:59
        Assert.False(ReglasEvento.Activo(evento, Fin));                    // 17:12:00

        Assert.Equal(7 * 60, ReglasEvento.SegundosRestantes(evento, Inicio));
        Assert.Equal("00:06:50", ReglasEvento.CuentaAtras(410));           // 17:05:09 -> quedan 6:50
        Assert.Equal(0, ReglasEvento.SegundosRestantes(evento, Fin.AddSeconds(1)));
    }

    // 4) El descuento lo decide el servidor y solo alcanza a los héroes que el evento LISTA.
    //    Mago: 1200 con 30 % -> 840 (no 850). Un héroe fuera de la lista no cambia.
    [Fact]
    public void PrecioConDescuento_SoloAlHeroeDeLaLista()
    {
        var evento = Campana();
        var ahora = Inicio.AddMinutes(1);

        Assert.Equal(840, ReglasEvento.PrecioConDescuento(evento, "mago", 1200, ahora));
        Assert.Equal(900, ReglasEvento.PrecioConDescuento(evento, "vampiro", 900, ahora));

        // Sin evento, el catálogo manda: el Mago vuelve a 1200.
        var apagado = Campana(activo: false);
        Assert.Equal(1200, ReglasEvento.PrecioConDescuento(apagado, "mago", 1200, ahora));

        // Después de la ventana tampoco hay descuento.
        Assert.Equal(1200, ReglasEvento.PrecioConDescuento(evento, "mago", 1200, Fin.AddSeconds(1)));
    }

    // 5) El botín exclusivo se SUMA a la tabla del S10 y la semilla del combate decide.
    //    La tabla base NO se toca: ConfigBotin sigue con sus tres premios.
    [Fact]
    public void BotinConEvento_SumaSinTocarLaTablaBase()
    {
        var baseS10 = new ConfigBotin();
        var original = baseS10.Premios.Count;                  // 3: espada / poción / nada
        var ahora = Inicio.AddMinutes(1);

        var conEvento = ReglasEvento.BotinConEvento(Campana(), baseS10, ahora);
        Assert.Equal(original + 1, conEvento.Premios.Count);
        Assert.Contains(conEvento.Premios, p => p.Id == "EMBLEMA_FESTIVAL");
        Assert.Equal(original, baseS10.Premios.Count);         // la base queda intacta
        Assert.DoesNotContain(baseS10.Premios, p => p.Id == "EMBLEMA_FESTIVAL");

        // Apagado el evento, la tabla es exactamente la del S10.
        var sinEvento = ReglasEvento.BotinConEvento(Campana(activo: false), baseS10, ahora);
        Assert.Equal(original, sinEvento.Premios.Count);

        // Con la misma semilla, el botín es el mismo; y en 300 combates el exclusivo cae.
        var primera = ReglasProgresion.TirarBotin(conEvento, 12345);
        var segunda = ReglasProgresion.TirarBotin(conEvento, 12345);
        Assert.Equal(primera.Count, segunda.Count);
        for (var i = 0; i < primera.Count; i++)
            Assert.Equal(primera[i].Id, segunda[i].Id);

        var cayeron = 0;
        for (var semilla = 1; semilla <= 300; semilla++)
            if (ReglasProgresion.TirarBotin(conEvento, semilla).Exists(p => p.Id == "EMBLEMA_FESTIVAL"))
                cayeron++;
        Assert.InRange(cayeron, 20, 100);                      // ~20 % de 300 = 60
    }

    // 6) El bonus de bienvenida se entrega UNA vez y solo a las cuentas nuevas
    //    (nivel de cuenta <= 1). La bandera impide el segundo cobro.
    [Fact]
    public void BonusBienvenida_UnaSolaVezYSoloParaNuevos()
    {
        var cfg = new ConfigBienvenida { Moneda = "ORO", Cantidad = 150 };

        Assert.True(ReglasEvento.EsNuevo(1));
        Assert.False(ReglasEvento.EsNuevo(2));

        Assert.Equal(150, ReglasEvento.BonoDeBienvenida(cfg, esNuevo: true, yaReclamado: false));
        Assert.Equal(0, ReglasEvento.BonoDeBienvenida(cfg, esNuevo: true, yaReclamado: true));   // ya se reclamó
        Assert.Equal(0, ReglasEvento.BonoDeBienvenida(cfg, esNuevo: false, yaReclamado: false)); // nivel 2

        // Sin override publicado la cantidad es 0: no hay bonus que dar.
        Assert.Equal(0, ReglasEvento.BonoDeBienvenida(new ConfigBienvenida(), true, false));
    }
}
