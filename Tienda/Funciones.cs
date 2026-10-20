using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace Tienda
{
    public class PublicarInventarioRequest { public List<string> Equipo; }

    // DI: cliente de servicios UGS para el módulo (patrón oficial de Cloud Code)
    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.AddGameApiClient();
        }
    }

    public static class Funciones
    {
        // ================= PASOS 3-5: BUSCAR RIVAL =================
        // Franja inicial ±2, paso 2, tope FranjaMaxima. Cada consulta y
        // cada entrada devuelta se cuentan (se pagan lecturas del índice).
        [CloudCodeFunction("BuscarRival")]
        public static async Task<BuscarRivalResultado> BuscarRival(IExecutionContext context, IGameApiClient api, BuscarRivalRequest req)
        {
            var res = new BuscarRivalResultado();

            // Si no ha publicado defensa (nivel<=0), no busca (tampoco puede ser atacado).
            if (req.Nivel <= 0)
            {
                res.Hubo = false;
                res.Mensaje = "No has publicado defensa (nivel <= 0): no buscas ni puedes ser atacado.";
                return res;
            }

            var candidatos = new List<Candidato>();
            int consultas = 0, lecturas = 0;

            for (int franja = 2; franja <= req.FranjaMaxima; franja += 2)
            {
                int nivelMinimo = req.Nivel - franja;
                int nivelMaximo = req.Nivel + franja;

                var campos = new List<FieldFilter>
                {
                    new FieldFilter("nivel", nivelMinimo, FieldFilter.OpEnum.GE, true),
                    new FieldFilter("nivel", nivelMaximo, FieldFilter.OpEnum.LE, true)
                };
                var consulta = new QueryIndexBody(campos, new List<string> { Llaves.Defensa, Llaves.Nivel }, 0, 20, 0);

                var r = await api.CloudSaveData.QueryPublicPlayerDataAsync(
                    context, context.ServiceToken, context.ProjectId, consulta);
                consultas++;

                // Parseo tolerantte del resultado: [{id, data:[{key,value}]}]
                var json = JsonConvert.SerializeObject(r.Data.Results ?? new List<QueryIndexResponseResultsInner>());
                var crudos = JsonConvert.DeserializeObject<List<CandidatoCrudo>>(json) ?? new List<CandidatoCrudo>();
                lecturas += crudos.Count;

                foreach (var c in crudos)
                {
                    string id = c.id == null ? "" : c.id.ToString();
                    DefensaPublica defensa = null;
                    int nivel = 0;
                    if (c.data != null)
                        foreach (var par in c.data)
                        {
                            if (par.key == Llaves.Defensa && par.value != null)
                                defensa = par.value.ToObject<DefensaPublica>();
                            else if (par.key == Llaves.Nivel && par.value != null)
                                nivel = Convert.ToInt32(((JValue)par.value).Value);
                        }
                    candidatos.Add(new Candidato { Id = id, Nivel = nivel, Defensa = defensa });
                }

                // Paso 5: excluir al propio jugador (y饪o no soy mi rival)
                candidatos.RemoveAll(c => c.Id == req.PlayerId);

                res.Franja = franja;
                if (candidatos.Count >= req.CandidatosSuficientes) break;
            }

            res.Consultas = consultas;
            res.LecturasConsumidas = lecturas;

            if (candidatos.Count == 0)
            {
                res.Hubo = false;
                res.Mensaje = $"No hay rivales en la franja final ±{res.Franja}.";
                return res;
            }

            // El más cercano en nivel
            var elegido = candidatos.OrderBy(c => Math.Abs(c.Nivel - req.Nivel)).First();
            res.Hubo = true;
            res.RivalPlayerId = elegido.Id;
            res.Rival = elegido.Defensa;
            return res;
        }

        // ================= SERVIDOR ESCRIBE EL INVENTARIO PROTEGIDO =================
        // [S07] El servidor graba la clave protegida "inventario" del jugador que pide.
        // El CLIENTE nunca puede escribir esto (está protegido).
        [CloudCodeFunction("PublicarInventario")]
        public static async Task<string> PublicarInventario(IExecutionContext context, IGameApiClient api, PublicarInventarioRequest req)
        {
            var lista = new List<HeroeInv>();
            if (req != null && req.Equipo != null)
                foreach (var id in req.Equipo)
                {
                    int vida = 100, ataque = 20;
                    if (TablaRivales.Heroes.TryGetValue(id, out var stats)) { vida = stats.vida; ataque = stats.ataque; }
                    lista.Add(new HeroeInv { id = id, vida = vida, ataque = ataque });
                }

            var inventario = new { heroes = lista };
            await api.CloudSaveData.SetProtectedItemAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId,
                new SetItemBody(Llaves.Inventario, inventario));
            return "inventario listo: " + lista.Count + " héroes";
        }

        // ================= PASO 6: RESOLVER COMBATE =================
        // Con un jugador real: su defensa es PÚBLICA (la escribió él) pero su
        // inventario es PROTEGIDO (lo escribió el servidor en PublicarInventario).
        // Lo que dice la defensa no vale de ningún héroe que no esté en el inventario.
        [CloudCodeFunction("ResolverCombate")]
        public static async Task<ResultadoCombate> ResolverCombate(IExecutionContext context, IGameApiClient api, ResolverCombateRequest req)
        {
            var npc = TablaRivales.NpcPorId(req.Rival);
            if (npc != null)
            {
                bool ganoNpc = MiPoder(req.Equipo) >= npc.Vida + npc.Ataque * 2;
                return new ResultadoCombate
                {
                    Gano = ganoNpc,
                    RivalNombre = npc.Nombre,
                    Fuente = "npc",
                    Oro = ganoNpc ? npc.Oro : 0,
                    Trofeos = ganoNpc ? npc.Trofeos : 0,
                    Mensaje = ganoNpc ? "Venciste al npc." : "El npc te venció."
                };
            }

            if (req.Rival == req.PlayerId)
                throw new Exception("No puedes pelear contra tu propia defensa.");

            // ---- Defensa pública del rival ----
            var defResp = await api.CloudSaveData.GetPublicItemsAsync(
                context, context.ServiceToken, context.ProjectId, req.Rival,
                new List<string> { Llaves.Defensa }, null);
            var defensa = LeerDefensa(defResp.Data.Results);
            if (defensa == null)
                throw new Exception($"No hay ningún rival <{req.Rival}>.");
            if (defensa.Equipo.Count == 0) // caso límite (paso 8): RESULTADO, no excepción
                return new ResultadoCombate
                {
                    Gano = false,
                    RivalNombre = defensa.Nombre,
                    Fuente = "pvp",
                    Mensaje = $"<{defensa.Nombre}> tiene una defensa vacía."
                };

            // ---- Inventario PROTEGIDO del rival (S06/S07) ----
            var invResp = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, req.Rival,
                new List<string> { Llaves.Inventario }, null);
            var declarados = defensa.Equipo.Select(h => h.Id).ToList();
            var inventario = LeerInventario(invResp.Data.Results);

            // HEROES_COLOSO declarado (no está en nadie) → rechaza la batalla
            if (declarados.Contains(Llaves.HeroeColoso))
                throw new Exception($"La defensa de {defensa.Nombre} declara {Llaves.HeroeColoso} (coloso): no es peleable.");

            var equipoRival = ArmarEquipo(declarados, inventario, out var ajeno);
            if (equipoRival == null)
                throw new Exception($"La defensa de {defensa.Nombre} declara {ajeno}, que no está en su inventario.");

            // ---- Resolver con stats de tabla (en el servidor, no el cliente) ----
            bool gano = MiPoder(req.Equipo) >= PoderDe(equipoRival);
            return new ResultadoCombate
            {
                Gano = gano,
                RivalNombre = defensa.Nombre,
                Fuente = "pvp",
                Oro = gano ? TablaRivales.OroPvp : 0,
                Trofeos = gano ? TablaRivales.TrofeosPvp : 0,
                Mensaje = gano
                    ? $"Venciste a {defensa.Nombre} (+{TablaRivales.OroPvp} oro, +{TablaRivales.TrofeosPvp} trofeos)."
                    : $"{defensa.Nombre} te venció."
            };
        }

        // ================= helpers =================
        class Candidato { public string Id; public int Nivel; public DefensaPublica Defensa; }
        class CandidatoCrudo { public JToken id; public List<Par> data; }
        class Par { public string key; public JToken value; }
        class HeroeInv { public string id; public int vida; public int ataque; }

        static DefensaPublica LeerDefensa(List<Item> items)
        {
            if (items == null) return null;
            foreach (var item in items)
                if (item.Key == Llaves.Defensa && item.Value != null)
                    return JsonConvert.DeserializeObject<DefensaPublica>(JsonConvert.SerializeObject(item.Value));
            return null;
        }

        static List<HeroeInv> LeerInventario(List<Item> items)
        {
            var lista = new List<HeroeInv>();
            if (items == null) return lista;
            foreach (var item in items)
                if (item.Key == Llaves.Inventario && item.Value != null)
                {
                    var json = JsonConvert.SerializeObject(item.Value);
                    var ind = JsonConvert.DeserializeObject<InventarioCrudo>(json);
                    if (ind != null && ind.heroes != null) lista.AddRange(ind.heroes);
                }
            return lista;
        }
        class InventarioCrudo { public List<HeroeInv> heroes; }

        static int MiPoder(List<string> equipo)
        {
            // Poder del towering equipo atacante: #héroes*100 + ataque promedio*10 (stats de tabla)
            if (equipo == null || equipo.Count == 0) return 0;
            int suma = 0;
            foreach (var id in equipo)
                suma += TablaRivales.Heroes.TryGetValue(id, out var s) ? s.ataque : 20;
            int promedio = suma / equipo.Count;
            return TablaRivales.Poder(equipo.Count, promedio);
        }

        static int PoderDe(List<(int vida, int ataque)> equipo)
        {
            if (equipo == null || equipo.Count == 0) return 0;
            int prom = (int)equipo.Average(h => h.ataque);
            return TablaRivales.Poder(equipo.Count, prom);
        }

        // Solo heroes DECLARADOS y PRESENTES en inventario; stats reales vienen del inventario.
        static List<(int, int)> ArmarEquipo(List<string> declarados, List<HeroeInv> inventario, out string ajeno)
        {
            ajeno = "";
            var equipo = new List<(int, int)>();
            if (declarados == null || inventario == null) return null;
            foreach (string id in declarados)
            {
                var inv = inventario.FirstOrDefault(h => h.id == id);
                if (inv == null) { ajeno = id; return null; }
                equipo.Add((inv.vida, inv.ataque));
            }
            return equipo;
        }
    }
}
