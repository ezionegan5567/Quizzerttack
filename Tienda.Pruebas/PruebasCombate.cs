using System.Collections.Generic;
using System.Linq;
using Tienda;
using Xunit;

// ============ LAB 08: LAS SIETE PRUEBAS DEL `dotnet test` ============
// Seis son de la SIMULACIÓN (puras, sin nube) y una de la CONFIGURACIÓN
// (la tabla de rivales apunta a héroes que existen en la tabla de héroes).

namespace Tienda.Pruebas
{
    public class PruebasCombate
    {
        static List<UnidadCombate> Equipo(params (string id, int vida, int ataque, int defensa, int vel)[] u)
        {
            return u.Select(x => new UnidadCombate
            {
                Id = x.id, Vida = x.vida, Ataque = x.ataque, Defensa = x.defensa, Velocidad = x.vel
            }).ToList();
        }

        static List<UnidadCombate> TresContraTres()
        {
            return Equipo(("a1", 140, 12, 9, 8), ("a2", 120, 18, 4, 12), ("a3", 100, 22, 2, 15));
        }

        static string Huella(ResultadoBatalla r)
        {
            return string.Join("|", r.BattleLog.Select(e =>
                $"{e.Turno}/{e.Atacante}/{e.Objetivo}/{e.Dano}/{e.VidaRestante}"));
        }

        // 1) La misma semilla y los mismos equipos dan el MISMO battle log.
        [Fact]
        public void MismaSemilla_MismoBattleLog()
        {
            var r1 = Batalla.Similar(TresContraTres(), TresContraTres(), 856593371);
            var r2 = Batalla.Similar(TresContraTres(), TresContraTres(), 856593371);

            Assert.Equal(Huella(r1), Huella(r2));
            Assert.Equal(r1.TurnosJugados, r2.TurnosJugados);
            Assert.Equal(r1.Victoria, r2.Victoria);
        }

        // 2) Cambiar la semilla cambia la pelea: el azar está sembrado, no es fijo.
        [Fact]
        public void SemillaDistinta_CambiaElLog()
        {
            var r1 = Batalla.Similar(TresContraTres(), TresContraTres(), 1);
            var r2 = Batalla.Similar(TresContraTres(), TresContraTres(), 2);

            Assert.NotEqual(Huella(r1), Huella(r2));
        }

        // 3) El orden de ataque sale de la velocidad (el más rápido golpea primero).
        [Fact]
        public void OrdenDeTurno_PorVelocidad()
        {
            var propio = Equipo(("lento", 200, 10, 0, 1), ("rapido", 200, 10, 0, 20));
            var rival = Equipo(("medio", 200, 10, 0, 10));

            var r = Batalla.Similar(propio, rival, 7);

            Assert.Equal("rapido", r.BattleLog[0].Atacante);
            Assert.Equal("medio", r.BattleLog[1].Atacante);
        }

        // 4) El dado resta como mucho 2 y el daño nunca baja de 1 aunque la defensa sea enorme.
        [Fact]
        public void DanoMinimoEsUno()
        {
            var propio = Equipo(("debil", 100, 1, 0, 5));
            var rival = Equipo(("muro", 40, 0, 99, 1)); // ataque 0: en teoría no haría daño

            var r = Batalla.Similar(propio, rival, 12345);

            foreach (var e in r.BattleLog)
            {
                Assert.True(e.Dano >= 1, $"daño {e.Dano} en el turno {e.Turno}");
                Assert.True(e.VidaRestante >= 0, "la vida nunca queda negativa");
            }
        }

        // 5) Simular no toca los equipos que se le pasan (se puede repetir la pelea).
        [Fact]
        public void Simular_NoMutaLosEquiposDeEntrada()
        {
            var propio = TresContraTres();
            var rival = TresContraTres();

            Batalla.Similar(propio, rival, 999);

            Assert.Equal(140, propio[0].Vida);
            Assert.Equal(120, propio[1].Vida);
            Assert.Equal(100, propio[2].Vida);
            Assert.All(propio, u => Assert.False(u.EsPropio));
            Assert.All(rival, u => Assert.False(u.EsPropio));
        }

        // 6) Si nadie cae, la pelea se corta en el tope de turnos (no es un bucle infinito).
        [Fact]
        public void TopeDeTurnos_CortaLaPelea()
        {
            var propio = Equipo(("tanque1", 100000, 1, 0, 5));
            var rival = Equipo(("tanque2", 100000, 1, 0, 4));

            var r = Batalla.Similar(propio, rival, 424242);

            Assert.Equal(Batalla.MaxTurnos, r.TurnosJugados);
            Assert.True(r.EquipoPropio[0].Vida > 0 && r.EquipoRival[0].Vida > 0);
        }

        // 7) La tabla de rivales solo puede mandar al campo héroes de la tabla de héroes.
        [Fact]
        public void TablaDeRivales_UsaHeroesDeLaTabla()
        {
            var t = TablaCombate.PorDefecto();

            Assert.NotEmpty(t.Rivales);
            Assert.All(t.Heroes.Values, h => Assert.True(h.Vida > 0 && h.Ataque > 0 && h.Velocidad > 0));
            foreach (var rival in t.Rivales)
            {
                Assert.InRange(rival.Equipo.Count, 1, 3);
                foreach (var id in rival.Equipo)
                    Assert.True(t.Heroes.ContainsKey(id), $"el rival {rival.Id} manda un héroe desconocido: {id}");
            }
        }
    }
}
