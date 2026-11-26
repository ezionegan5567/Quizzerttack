// ============ LAB14 · FASE 1: PRUEBAS DE PROGRESIÓN Y TROFEOS ============
// Reglas puras del S10/S11: XP, niveles, botín, nodos y trofeos.
using System.Collections.Generic;
using System.Linq;
using Tienda;
using Xunit;

namespace Tienda.Pruebas
{
    public class ProgresionPruebas
    {
        readonly ConfigExperiencia _xp = new ConfigExperiencia
        {
            Victoria = 50, Derrota = 15, PorTurno = 1, TopeTurnos = 20
        };

        readonly ConfigNiveles _niveles = new ConfigNiveles { Maximo = 10, CostoBase = 100 };

        [Fact]
        public void Victoria_SumaBaseYPorTurno()
        {
            Assert.Equal(61, ReglasProgresion.ExperienciaDeCombate(_xp, true, 11));
        }

        [Fact]
        public void LosTurnosSeCortanEnElTope()
        {
            Assert.Equal(70, ReglasProgresion.ExperienciaDeCombate(_xp, true, 50));
        }

        [Fact]
        public void Derrota_DaMenosExperiencia()
        {
            Assert.Equal(20, ReglasProgresion.ExperienciaDeCombate(_xp, false, 5));
        }

        [Fact]
        public void CostoDeSubir_CreceConElCuadradoDelNivel()
        {
            Assert.Equal(100, ReglasProgresion.CostoParaSubir(_niveles, 1));
            Assert.Equal(400, ReglasProgresion.CostoParaSubir(_niveles, 2));
            Assert.Equal(2500, ReglasProgresion.CostoParaSubir(_niveles, 5));
        }

        [Fact]
        public void SubeVariosNivelesYConservaElSobrante()
        {
            var (nivel, experiencia, subidos) = ReglasProgresion.AplicarNiveles(_niveles, 1, 90, 600);

            Assert.Equal(3, nivel);
            Assert.Equal(190, experiencia);
            Assert.Equal(2, subidos);
        }

        [Fact]
        public void EnElNivelMaximo_NoSubeMas()
        {
            var (nivel, experiencia, subidos) = ReglasProgresion.AplicarNiveles(_niveles, 10, 50, 9999);

            Assert.Equal(10, nivel);
            Assert.Equal(0, experiencia);
            Assert.Equal(0, subidos);
        }

        [Fact]
        public void ElBotinConLaMismaSemilla_EsElMismo()
        {
            var botin = new ConfigBotin
            {
                Tiradas = 3,
                Premios = new List<PremioBotin>
                {
                    new PremioBotin { Id = "espada", Cantidad = 1, Peso = 40 },
                    new PremioBotin { Id = "pocion", Cantidad = 2, Peso = 40 },
                    new PremioBotin { Id = "nada", Cantidad = 0, Peso = 20 }
                }
            };

            var a = ReglasProgresion.TirarBotin(botin, 8080);
            var b = ReglasProgresion.TirarBotin(botin, 8080);

            Assert.Equal(a.Select(p => p.Id), b.Select(p => p.Id));
            Assert.Equal(3, a.Count);
        }

        [Fact]
        public void SinPremios_ElBotinEsVacio()
        {
            Assert.Empty(ReglasProgresion.TirarBotin(null, 1));
            Assert.Empty(ReglasProgresion.TirarBotin(new ConfigBotin { Premios = new List<PremioBotin>() }, 1));
        }

        [Fact]
        public void AlGanar_LosTrofeosSumanLosDelRival()
        {
            Assert.Equal(20, ReglasProgresion.CambioDeTrofeos(5, true, 20));
        }

        [Fact]
        public void AlPerder_SoloSeRestaLaMitadDelRival()
        {
            Assert.Equal(-10, ReglasProgresion.CambioDeTrofeos(50, false, 20));
        }

        [Fact]
        public void AlPerder_NuncaSeBajaDeCero()
        {
            Assert.Equal(-5, ReglasProgresion.CambioDeTrofeos(5, false, 20));
            Assert.Equal(0, ReglasProgresion.CambioDeTrofeos(0, false, 20));
        }

        [Fact]
        public void ElTierSaleDelPuesto()
        {
            Assert.Equal("Oro", ReglasProgresion.TierPorPuesto(1));
            Assert.Equal("Oro", ReglasProgresion.TierPorPuesto(2));
            Assert.Equal("Plata", ReglasProgresion.TierPorPuesto(3));
            Assert.Equal("Plata", ReglasProgresion.TierPorPuesto(9));
            Assert.Equal("Bronce", ReglasProgresion.TierPorPuesto(10));
            Assert.Equal("", ReglasProgresion.TierPorPuesto(0));
        }

        [Fact]
        public void ElPremioDeTemporada_NoDistingueMayusculas()
        {
            var cfg = new ConfigTemporada
            {
                Premios = new Dictionary<string, PremioTemporada>
                {
                    { "Oro", new PremioTemporada { Nombre = "Corona de oro", Gemas = 20 } }
                }
            };

            Assert.Equal(20, ReglasProgresion.PremioDe(cfg, "oro").Gemas);
        }

        [Fact]
        public void NodoSinPrerrequisitos_FaltaDesbloquearAntes()
        {
            var nodo = new ConfigNodo
            {
                Id = "MAESTRIA",
                Requiere = new List<string> { "TALENTO_ATAQUE_2" },
                NivelCuentaMin = 1, CuestaMoneda = 0
            };

            var motivo = ReglasProgresion.PorQueNoSePuede(nodo, new List<string>(), 5, 999);

            Assert.Contains("TALENTO_ATAQUE_2", motivo);
        }

        [Fact]
        public void NodoYaDesbloqueado_NoSeCobraDosVeces()
        {
            var nodo = new ConfigNodo { Id = "TALENTO_ATAQUE_1", NivelCuentaMin = 1, CuestaMoneda = 100 };

            Assert.Equal("ya está desbloqueado",
                ReglasProgresion.PorQueNoSePuede(nodo, new List<string> { "TALENTO_ATAQUE_1" }, 1, 999));
        }

        [Fact]
        public void NodoPideNivelDeCuenta()
        {
            var nodo = new ConfigNodo { Id = "TALENTO_ATAQUE_2", NivelCuentaMin = 4, CuestaMoneda = 300 };

            Assert.Contains("nivel de cuenta 4",
                ReglasProgresion.PorQueNoSePuede(nodo, new List<string>(), 2, 999));
        }

        [Fact]
        public void NodoPideOro()
        {
            var nodo = new ConfigNodo { Id = "TALENTO_DEFENSA_1", NivelCuentaMin = 1, CuestaMoneda = 150 };

            Assert.Contains("Falta oro",
                ReglasProgresion.PorQueNoSePuede(nodo, new List<string>(), 1, 100));
        }

        [Fact]
        public void NodoConTodoEnOrden_Procede()
        {
            var nodo = new ConfigNodo { Id = "TALENTO_DEFENSA_1", NivelCuentaMin = 1, CuestaMoneda = 150 };

            Assert.Null(ReglasProgresion.PorQueNoSePuede(nodo, new List<string>(), 1, 150));
        }
    }
}
