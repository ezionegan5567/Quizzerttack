// ============ LAB14 · FASE 1: PRUEBAS DE LIVE OPS (evento y descuentos) ============
using System;
using System.Collections.Generic;
using Tienda;
using Xunit;

namespace Tienda.Pruebas
{
    public class EventoPruebas
    {
        static readonly DateTime Ahora = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        static ConfigEvento Evento(bool activo = true, string inicio = "", string fin = "")
        {
            return new ConfigEvento
            {
                Activo = activo,
                InicioUtc = inicio,
                FinUtc = fin,
                MultiplicadorDano = 1.25,
                DescuentoTienda = 0.30,
                HeroesEspeciales = new List<string> { "mago" }
            };
        }

        [Fact]
        public void EventoApagado_NoAplicaNada()
        {
            var cfg = Evento(activo: false);

            Assert.False(ReglasEvento.Activo(cfg, Ahora));
            Assert.Equal(1.0, ReglasEvento.FactorDano(cfg, Ahora));
        }

        [Fact]
        public void AntesDelInicio_NoEstaActivo()
        {
            var cfg = Evento(inicio: "2026-11-21T00:00:00Z", fin: "2026-11-25T00:00:00Z");

            Assert.False(ReglasEvento.Activo(cfg, Ahora));
        }

        [Fact]
        public void DentroDeLaVentana_EstaActivo()
        {
            var cfg = Evento(inicio: "2026-11-19T00:00:00Z", fin: "2026-11-25T00:00:00Z");

            Assert.True(ReglasEvento.Activo(cfg, Ahora));
        }

        [Fact]
        public void EnElInstanteDeFin_YaNoEstaActivo()
        {
            var cfg = Evento(inicio: "2026-11-19T00:00:00Z", fin: "2026-11-20T12:00:00Z");

            Assert.False(ReglasEvento.Activo(cfg, Ahora));
        }

        [Fact]
        public void ElAtaqueSeEscalaYRedondeaEnElServidor()
        {
            Assert.Equal(15, ReglasEvento.AtaqueEscalado(12, 1.25));
        }

        [Fact]
        public void ElAtaqueEscaladoNuncaEsCero()
        {
            Assert.Equal(1, ReglasEvento.AtaqueEscalado(0, 0.5));
        }

        [Fact]
        public void ElDescuentoSoloAplicaaLosHeroesListados()
        {
            var cfg = Evento();

            Assert.Equal(840, ReglasEvento.PrecioConDescuento(cfg, "mago", 1200, Ahora));
            Assert.Equal(1200, ReglasEvento.PrecioConDescuento(cfg, "vampiro", 1200, Ahora));
        }

        [Fact]
        public void ElDescuentoNoPasaDelCienPorCiento()
        {
            var cfg = Evento();
            cfg.DescuentoTienda = 5.0;

            Assert.Equal(0, ReglasEvento.PrecioConDescuento(cfg, "mago", 1200, Ahora));
        }

        [Fact]
        public void ElSegmentoNuevo_LoDecideElServidor()
        {
            Assert.True(ReglasEvento.EsNuevo(1));
            Assert.False(ReglasEvento.EsNuevo(2));
        }

        [Fact]
        public void ElBonusDeBienvenida_SeEntregaUnaSolaVez()
        {
            var cfg = new ConfigBienvenida { Moneda = "ORO", Cantidad = 500 };

            Assert.Equal(500, ReglasEvento.BonoDeBienvenida(cfg, true, false));
            Assert.Equal(0, ReglasEvento.BonoDeBienvenida(cfg, true, true));
            Assert.Equal(0, ReglasEvento.BonoDeBienvenida(cfg, false, false));
        }

        [Fact]
        public void LaCuentaAtrasSeFormateaEnHoras()
        {
            Assert.Equal("01:01:01", ReglasEvento.CuentaAtras(3661));
            Assert.Equal("00:00:00", ReglasEvento.CuentaAtras(-5));
        }

        [Fact]
        public void ElBotinDelEventoSeSumaAlBase()
        {
            var cfg = Evento();
            cfg.BotinExclusivo = new List<PremioBotin>
            {
                new PremioBotin { Id = "reliquia", Tipo = "objeto", Cantidad = 1, Peso = 10 }
            };
            var baseBotin = new ConfigBotin
            {
                Tiradas = 1,
                Premios = new List<PremioBotin> { new PremioBotin { Id = "espada", Peso = 90 } }
            };

            var conEvento = ReglasEvento.BotinConEvento(cfg, baseBotin, Ahora);

            Assert.Equal(2, conEvento.Premios.Count);
            Assert.Single(baseBotin.Premios); // la base NO se toca
        }
    }
}
