using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

// ============ LAB 10: SIMULACIÓN DETERMINISTA + ESTADO DEL JUGADOR ============
// Simular() es PURA y determinista: la misma semilla + los mismos equipos producen
// SIEMPRE el mismo battle log, los mismos turnos y la misma VidaFinalPropio. Eso
// permite (paso 8) reconstruir el último combate en un reintentón sin recalcular
// premios, y (paso 5) que el botín use una corriente de azar APARTE (semilla ^ 0x5EED)
// para no alterar el log.

namespace Tienda
{
    // ---------- Llaves protegidas que toca el combate con progresión (6 en total) ----------
    public static class LlavesProgresion
    {
        public const string Progreso = "progreso";           // {trofeos, desbloqueos}
        public const string Perfil = "perfil";               // {nivelCuenta, experiencia}
        public const string Saldos = "saldos";               // {monedas}
        public const string Operaciones = "operaciones";     // {Ids} (últimos 20) -> idempotencia
        public const string UltimoCombate = "ultimo_combate";// {semilla, equipos, log, ...}
        // + Llaves.Inventario (ya existe) -> héroes con nivel/experiencia + objetos del botín
    }

    // ---------- Estado del jugador (clase PROTECTED: solo el servidor escribe) ----------
    public class HeroeEstado
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("vida")] public int Vida { get; set; } = 100;
        [JsonProperty("ataque")] public int Ataque { get; set; } = 20;
        [JsonProperty("nivel")] public int Nivel { get; set; } = 1;
        [JsonProperty("experiencia")] public int Experiencia { get; set; } = 0;
    }

    public class InventarioEstado { [JsonProperty("heroes")] public List<HeroeEstado> Heroes { get; set; } = new List<HeroeEstado>(); }
    public class PerfilEstado { [JsonProperty("nivelCuenta")] public int NivelCuenta { get; set; } = 1; [JsonProperty("experiencia")] public int Experiencia { get; set; } = 0; }
    public class ProgresoEstado { [JsonProperty("trofeos")] public int Trofeos { get; set; } = 0; [JsonProperty("desbloqueos")] public List<string> Desbloqueos { get; set; } = new List<string>(); }
    public class SaldosEstado { [JsonProperty("monedas")] public int Monedas { get; set; } = 0; }
    public class OperacionesEstado { [JsonProperty("Ids")] public List<string> Ids { get; set; } = new List<string>(); }

    public class UltimoCombateEstado
    {
        [JsonProperty("semilla")] public int Semilla { get; set; }
        [JsonProperty("victoria")] public bool Victoria { get; set; }
        [JsonProperty("turnos")] public int Turnos { get; set; }
        [JsonProperty("equipos")] public List<string> Equipos { get; set; } = new List<string>();
        [JsonProperty("log")] public List<string> Log { get; set; } = new List<string>();
        [JsonProperty("experiencia")] public int Experiencia { get; set; }
        [JsonProperty("subieron")] public List<string> Subieron { get; set; } = new List<string>();
        [JsonProperty("botin")] public List<string> Botin { get; set; } = new List<string>();
    }

    // Agregado que se serializa en las 6 llaves protegidas.
    public class EstadoJugador
    {
        public InventarioEstado Inventario { get; set; } = new InventarioEstado();
        public OperacionesEstado Operaciones { get; set; } = new OperacionesEstado();
        public PerfilEstado Perfil { get; set; } = new PerfilEstado();
        public ProgresoEstado Progreso { get; set; } = new ProgresoEstado();
        public SaldosEstado Saldos { get; set; } = new SaldosEstado();
        public UltimoCombateEstado UltimoCombate { get; set; } = new UltimoCombateEstado();
    }

    // ---------- Resultado de la simulación ----------
    public class ResultadoSimulacion
    {
        public bool Victoria { get; set; }
        public int TurnosJugados { get; set; }
        public int[] VidaFinalPropio { get; set; } = new int[0]; // 0 = caído
        public List<string> Log { get; set; } = new List<string>();
        public int Semilla { get; set; }
    }

    public static class Simulador
    {
        // stats efectivas escaladas por nivel (10% por nivel sobre el base)
        static int VidaEfectiva(HeroeEstado h) { return (int)Math.Round(h.Vida * (1 + 0.10 * Math.Max(0, h.Nivel - 1))); }
        static int AtaqueEfectivo(HeroeEstado h) { return (int)Math.Round(h.Ataque * (1 + 0.10 * Math.Max(0, h.Nivel - 1))); }

        static int ObjetivoVivo(List<int> pv, Random azar)
        {
            var vivos = new List<int>();
            for (int i = 0; i < pv.Count; i++) if (pv[i] > 0) vivos.Add(i);
            if (vivos.Count == 0) return -1;
            return vivos[azar.Next(vivos.Count)];
        }

        // Simulación por turnos, determinista por semilla. maxTurnos corta la pelea (empate a puntos por vida).
        public static ResultadoSimulacion Simular(List<HeroeEstado> propios, List<HeroeEstado> rivales, int semilla, int maxTurnos = 30)
        {
            propios = propios ?? new List<HeroeEstado>();
            rivales = rivales ?? new List<HeroeEstado>();
            var azar = new Random(semilla);

            var pvPropio = propios.Select(VidaEfectiva).ToList();
            var pvRival = rivales.Select(VidaEfectiva).ToList();
            var atPropio = propios.Select(AtaqueEfectivo).ToList();
            var atRival = rivales.Select(AtaqueEfectivo).ToList();
            var log = new List<string>();
            int turnos = 0;

            while (turnos < maxTurnos && pvPropio.Any(v => v > 0) && pvRival.Any(v => v > 0))
            {
                turnos++;
                for (int i = 0; i < pvPropio.Count; i++)
                {
                    if (pvPropio[i] <= 0) continue;
                    int obj = ObjetivoVivo(pvRival, azar);
                    if (obj < 0) break;
                    pvRival[obj] -= atPropio[i];
                    log.Add($"T{turnos}: {propios[i].Id} golpea a {rivales[obj].Id} (-{atPropio[i]})");
                }
                for (int j = 0; j < pvRival.Count; j++)
                {
                    if (pvRival[j] <= 0) continue;
                    int obj = ObjetivoVivo(pvPropio, azar);
                    if (obj < 0) break;
                    pvPropio[obj] -= atRival[j];
                    log.Add($"T{turnos}: {rivales[j].Id} golpea a {propios[obj].Id} (-{atRival[j]})");
                }
            }

            bool quedanRivales = pvRival.Any(v => v > 0);
            bool quedanPropios = pvPropio.Any(v => v > 0);
            bool victoria = !quedanRivales && quedanPropios;

            return new ResultadoSimulacion
            {
                Victoria = victoria,
                TurnosJugados = turnos,
                VidaFinalPropio = pvPropio.Select(v => Math.Max(0, v)).ToArray(),
                Log = log,
                Semilla = semilla
            };
        }
    }
}
