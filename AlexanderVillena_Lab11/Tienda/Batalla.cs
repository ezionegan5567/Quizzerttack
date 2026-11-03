using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

// ============ LAB 08: SIMULADOR DETERMINISTA DE REFERENCIA (pasos 4 y 5) ============
// Similar() es PURA: la misma semilla + los mismos equipos producen SIEMPRE el mismo
// battle log. NO lee nada de la nube y NO toca estado, por eso se puede probar sin
// desplegar (las 6 pruebas de la simulación del `dotnet test`).
// El servidor es la ÚNICA fuente de verdad: aquí nacen el daño y el resultado.

namespace Tienda
{
    // Una línea del battle log: quién pega, a quién, cuánto y cuánta vida queda.
    // Todo lo que el cliente necesita para dibujar la pelea sin recalcular nada.
    public class EntradaBatallaLog
    {
        [JsonProperty("turno")] public int Turno { get; set; }
        [JsonProperty("atacante")] public string Atacante { get; set; } = "";
        [JsonProperty("objetivo")] public string Objetivo { get; set; } = "";
        [JsonProperty("dano")] public int Dano { get; set; }
        [JsonProperty("vidaRestante")] public int VidaRestante { get; set; }
    }

    // Un combatiente como lo ve la simulación (y como viaja en EquipoPropio/EquipoRival).
    public class UnidadCombate
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("vida")] public int Vida { get; set; }
        [JsonProperty("ataque")] public int Ataque { get; set; }
        [JsonProperty("defensa")] public int Defensa { get; set; }
        [JsonProperty("velocidad")] public int Velocidad { get; set; }

        // Bando: no se serializa, se marca antes de simular (true = mis héroes).
        [JsonIgnore] public bool EsPropio { get; set; }

        public bool EstaViva() { return Vida > 0; }

        public UnidadCombate Clonar()
        {
            return new UnidadCombate
            {
                Id = Id, Vida = Vida, Ataque = Ataque,
                Defensa = Defensa, Velocidad = Velocidad, EsPropio = EsPropio
            };
        }
    }

    public class ResultadoBatalla
    {
        public bool Victoria { get; set; }
        public int TurnosJugados { get; set; }
        public int Semilla { get; set; }
        public List<UnidadCombate> EquipoPropio { get; set; } = new List<UnidadCombate>();
        public List<UnidadCombate> EquipoRival { get; set; } = new List<UnidadCombate>();
        public List<EntradaBatallaLog> BattleLog { get; set; } = new List<EntradaBatallaLog>();
    }

    public static class Batalla
    {
        // Tope de turnos: si nadie cae, gana quien tenga más vida entera al final.
        public const int MaxTurnos = 100;

        // paso 4 y 5: el bucle de turnos por velocidad, el dado y el battle log.
        public static ResultadoBatalla Similar(
            List<UnidadCombate> propio, List<UnidadCombate> rival, int semilla)
        {
            // Copias: el que llama puede repetir la simulación con las mismas unidades.
            var nuestros = (propio ?? new List<UnidadCombate>()).Select(u => u.Clonar()).ToList();
            var suyos = (rival ?? new List<UnidadCombate>()).Select(u => u.Clonar()).ToList();
            foreach (var u in nuestros) u.EsPropio = true;
            foreach (var u in suyos) u.EsPropio = false;

            // Los equipos que ENTRARON (vida inicial): el cliente pinta las barras desde aquí.
            var entraronPropios = nuestros.Select(u => u.Clonar()).ToList();
            var entraronRivales = suyos.Select(u => u.Clonar()).ToList();

            var azar = new Random(semilla); // el azar, SEMBRADO
            var orden = nuestros.Concat(suyos)
                                .OrderByDescending(u => u.Velocidad) // orden estable
                                .ToList();

            int turno = 0;
            var log = new List<EntradaBatallaLog>();

            while (turno < MaxTurnos && Viven(nuestros) && Viven(suyos))
            {
                turno++;
                foreach (var unidad in orden)
                {
                    if (!unidad.EstaViva()) continue;

                    var enemigos = unidad.EsPropio ? suyos : nuestros;
                    if (!Viven(enemigos)) break;

                    var objetivo = enemigos.First(u => u.EstaViva());
                    int dano = Math.Max(1, unidad.Ataque + azar.Next(-2, 3) - objetivo.Defensa);
                    objetivo.Vida = Math.Max(0, objetivo.Vida - dano);

                    log.Add(new EntradaBatallaLog
                    {
                        Turno = turno,
                        Atacante = unidad.Id,
                        Objetivo = objetivo.Id,
                        Dano = dano,
                        VidaRestante = objetivo.Vida
                    });
                }
            }

            bool vivenNuestros = Viven(nuestros);
            bool vivenSuyos = Viven(suyos);
            bool victoria;
            if (vivenNuestros && vivenSuyos)
                victoria = VidaEnPie(nuestros) > VidaEnPie(suyos); // se agotaron los turnos
            else
                victoria = vivenNuestros && !vivenSuyos;

            return new ResultadoBatalla
            {
                Victoria = victoria,
                TurnosJugados = turno,
                Semilla = semilla,
                EquipoPropio = entraronPropios,
                EquipoRival = entraronRivales,
                BattleLog = log
            };
        }

        static bool Viven(List<UnidadCombate> equipo) { return equipo.Any(u => u.EstaViva()); }

        static int VidaEnPie(List<UnidadCombate> equipo) { return equipo.Sum(u => Math.Max(0, u.Vida)); }
    }
}
