// ============ LAB14 · FASE 1: PRUEBAS DE LA SIMULACIÓN (sin nube) ============
// Batalla.Similar es PURA: misma semilla + mismos equipos = mismo battle log.
// Estas pruebas son las que corren en el script de despliegue y abortan el
// despliegue si fallan.
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Tienda;
using Xunit;

namespace Tienda.Pruebas
{
    public class BatallaPruebas
    {
        static List<UnidadCombate> Equipo(int vida, int ataque, int defensa, int velocidad, string id)
        {
            return new List<UnidadCombate>
            {
                new UnidadCombate
                {
                    Id = id, Vida = vida, Ataque = ataque,
                    Defensa = defensa, Velocidad = velocidad
                }
            };
        }

        [Fact]
        public void MismaSemilla_ProduceElMismoBattleLog()
        {
            var propio = Equipo(120, 30, 5, 10, "guerrero");
            var rival = Equipo(100, 20, 4, 9, "lanza_con_escudo");

            var a = Batalla.Similar(propio, rival, 12345);
            var b = Batalla.Similar(propio, rival, 12345);

            Assert.Equal(
                JsonConvert.SerializeObject(a.BattleLog),
                JsonConvert.SerializeObject(b.BattleLog));
            Assert.Equal(a.Victoria, b.Victoria);
            Assert.Equal(a.TurnosJugados, b.TurnosJugados);
        }

        [Fact]
        public void DistintaSemilla_PuedeCambiarElBattleLog()
        {
            var propio = Equipo(120, 30, 5, 10, "guerrero");
            var rival = Equipo(100, 20, 4, 9, "lanza_con_escudo");

            var a = Batalla.Similar(propio, rival, 1);
            var b = Batalla.Similar(propio, rival, 2);

            Assert.NotEqual(
                JsonConvert.SerializeObject(a.BattleLog),
                JsonConvert.SerializeObject(b.BattleLog));
        }

        [Fact]
        public void EquipoFuerte_GanaYElRivalQuedaCaido()
        {
            var propio = Equipo(500, 200, 20, 20, "coloso_propio");
            var rival = Equipo(50, 5, 0, 1, "debil");

            var r = Batalla.Similar(propio, rival, 7);

            Assert.True(r.Victoria);
            // El resultado guarda el equipo DE ENTRADA; el final se lee en el battle log.
            Assert.Equal(50, r.EquipoRival[0].Vida);
            Assert.Equal(0, r.BattleLog.Last(e => e.Objetivo == "debil").VidaRestante);
        }

        [Fact]
        public void EquipoDebil_Pierde()
        {
            var propio = Equipo(10, 1, 0, 1, "debil");
            var rival = Equipo(500, 200, 20, 20, "coloso_rival");

            var r = Batalla.Similar(propio, rival, 7);

            Assert.False(r.Victoria);
            Assert.Equal(0, r.BattleLog.Last(e => e.Objetivo == "debil").VidaRestante);
        }

        [Fact]
        public void NuncaPasaDelTopeDeTurnos()
        {
            // Defensas gigantes: nadie cae y se agota el tope.
            var propio = Equipo(1000, 1, 999, 5, "muro_propio");
            var rival = Equipo(1000, 1, 999, 5, "muro_rival");

            var r = Batalla.Similar(propio, rival, 3);

            Assert.True(r.TurnosJugados <= Batalla.MaxTurnos);
        }

        [Fact]
        public void ElDanoNuncaBajaDeUno()
        {
            var propio = Equipo(100, 1, 0, 10, "mino");
            var rival = Equipo(100, 1, 999, 1, "tanque");

            var r = Batalla.Similar(propio, rival, 11);

            Assert.All(r.BattleLog, e => Assert.True(e.Dano >= 1));
        }

        [Fact]
        public void NoModificaLasUnidadesQueRecibe()
        {
            var propio = Equipo(120, 30, 5, 10, "guerrero");
            var rival = Equipo(100, 20, 4, 9, "rival");

            Batalla.Similar(propio, rival, 99);

            Assert.Equal(120, propio[0].Vida);
            Assert.Equal(100, rival[0].Vida);
        }

        [Fact]
        public void EquiposVacios_NoLanzaExcepcion()
        {
            var r = Batalla.Similar(null, null, 5);

            Assert.False(r.Victoria);
            Assert.Empty(r.BattleLog);
        }
    }
}
