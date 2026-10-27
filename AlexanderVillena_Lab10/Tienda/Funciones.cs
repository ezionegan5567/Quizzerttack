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
using Unity.Services.RemoteConfig.Model;

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

    // OJO: Cloud Code NO soporta funciones static. La clase y los [CloudCodeFunction]
    // deben ser de INSTANCIA (los helpers internos sí pueden ser static).
    public class Funciones
    {
        // ================= PASOS 3-5: BUSCAR RIVAL =================
        // Franja inicial ±2, paso 2, tope FranjaMaxima. Cada consulta y
        // cada entrada devuelta se cuentan (se pagan lecturas del índice).
        [CloudCodeFunction("BuscarRival")]
        public async Task<BuscarRivalResultado> BuscarRival(IExecutionContext context, IGameApiClient api, BuscarRivalRequest req)
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
        public async Task<string> PublicarInventario(IExecutionContext context, IGameApiClient api, PublicarInventarioRequest req)
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
        public async Task<ResultadoCombate> ResolverCombate(IExecutionContext context, IGameApiClient api, ResolverCombateRequest req)
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

        // ================= LAB 10: COMBATE CON PROGRESION (paso 6) =================
        // Combate parcial + progresion que vive en el servidor. Toca las 6 llaves
        // PROTEGIDAS (inventario, operaciones, perfil, progreso, saldos, ultimo_combate)
        // en UN solo batch para que el cobro y la progresion sean atomicos e idempotentes.
        [CloudCodeFunction("ResolverCombateProgresion")]
        public async Task<ResultadoCombate> ResolverCombateProgresion(IExecutionContext context, IGameApiClient api, ResolverCombateRequest req)
        {
            req = req ?? new ResolverCombateRequest(); // tolerar un cuerpo vacio sin lanzar 422
            var cfg = await LeerConfiguracionAsync(context, api);
            var estado = await LeerEstadoAsync(context, api, context.PlayerId);

            // 2a. idempotencia: si la operacion ya se atendio, mismo resultado sin volver a premiar.
            if (!string.IsNullOrEmpty(req.IdOperacion) && estado.Operaciones.Ids.Contains(req.IdOperacion))
                return RespuestaRepetida(estado);

            // Equipos como HeroeEstado (los propios traen nivel/experiencia reales del inventario).
            var propios = ArmarPropios(estado.Inventario, req.Equipo);
            var npc = TablaRivales.NpcPorId(req.Rival);
            List<HeroeEstado> rivales;
            string rivalNombre;
            if (npc != null)
            {
                rivales = new List<HeroeEstado> { new HeroeEstado { Id = npc.Id, Vida = npc.Vida, Ataque = npc.Ataque } };
                rivalNombre = npc.Nombre;
            }
            else
            {
                if (req.Rival == context.PlayerId)
                    throw new Exception("No puedes pelear contra tu propia defensa.");
                var defResp = await api.CloudSaveData.GetPublicItemsAsync(
                    context, context.ServiceToken, context.ProjectId, req.Rival,
                    new List<string> { Llaves.Defensa }, null, default);
                var defensa = LeerDefensa(defResp.Data.Results);
                if (defensa == null) throw new Exception($"No hay ningun rival <{req.Rival}>.");
                var invResp = await api.CloudSaveData.GetProtectedItemsAsync(
                    context, context.ServiceToken, context.ProjectId, req.Rival,
                    new List<string> { Llaves.Inventario }, null, default);
                rivales = ArmarRivales(defensa.Equipo.Select(h => h.Id).ToList(), LeerInventario(invResp.Data.Results));
                rivalNombre = defensa.Nombre;
            }

            int semilla = req.Semilla != 0 ? req.Semilla : System.Environment.TickCount;
            var sim = Simulador.Simular(propios, rivales, semilla);

            // XP de cuenta y por heroes supervivientes (los caidos no ganan si caidosGanan=false).
            int xp = ReglasProgresion.ExperienciaDeCombate(cfg.Experiencia, sim.Victoria, sim.TurnosJugados);
            var subieron = new List<string>();
            for (int i = 0; i < propios.Count; i++)
            {
                if (!cfg.Experiencia.CaidosGanan && i < sim.VidaFinalPropio.Length && sim.VidaFinalPropio[i] <= 0)
                    continue;
                var (n, e, s) = ReglasProgresion.AplicarNiveles(cfg.Niveles, propios[i].Nivel, propios[i].Experiencia, xp);
                if (s > 0) subieron.Add(propios[i].Id);
                propios[i].Nivel = n; propios[i].Experiencia = e;
            }
            var (cn, ce, _) = ReglasProgresion.AplicarNiveles(cfg.Niveles, estado.Perfil.NivelCuenta, estado.Perfil.Experiencia, xp);
            estado.Perfil.NivelCuenta = cn; estado.Perfil.Experiencia = ce;

            // Botin solo al ganar, con corriente de azar aparte (semilla ^ 0x5EED dentro de TirarBotin).
            var botin = new List<string>();
            int monedasBotin = 0;
            if (sim.Victoria)
                foreach (var p in ReglasProgresion.TirarBotin(cfg.Botin, semilla))
                {
                    botin.Add(p.Id);
                    if (p.Tipo == "moneda") monedasBotin += p.Cantidad;
                }

            int oro = 0, trofeos = 0;
            if (sim.Victoria)
            {
                if (npc != null) { oro = npc.Oro; trofeos = npc.Trofeos; }
                else { oro = TablaRivales.OroPvp; trofeos = TablaRivales.TrofeosPvp; }
            }
            estado.Saldos.Monedas += oro + monedasBotin;
            estado.Progreso.Trofeos += trofeos;

            if (!string.IsNullOrEmpty(req.IdOperacion))
            {
                estado.Operaciones.Ids.Add(req.IdOperacion);
                while (estado.Operaciones.Ids.Count > 20) estado.Operaciones.Ids.RemoveAt(0);
            }

            estado.UltimoCombate = new UltimoCombateEstado
            {
                Semilla = semilla, Victoria = sim.Victoria, Turnos = sim.TurnosJugados,
                Equipos = req.Equipo ?? new List<string>(), Log = sim.Log, Experiencia = xp,
                Subieron = subieron, Botin = botin
            };
            AplicarInventario(estado.Inventario, propios);

            await EscribirEstadoBatchAsync(context, api, estado);

            return new ResultadoCombate
            {
                Gano = sim.Victoria, RivalNombre = rivalNombre, Fuente = npc != null ? "npc" : "pvp",
                Oro = oro + monedasBotin, Trofeos = trofeos, Xp = xp, Nivel = estado.Perfil.NivelCuenta,
                Subieron = subieron, Botin = botin, Repetida = false,
                Mensaje = sim.Victoria
                    ? $"Venciste a {rivalNombre} (+{xp} xp, +{oro + monedasBotin} oro)."
                    : $"{rivalNombre} te vencio (+{xp} xp)."
            };
        }

        // ================= LAB 10: DESBLOQUEAR NODO (paso 9) =================
        // Solo viaja el id; requisitos, nivel de cuenta y precio salen de la config y del estado PROTEGIDO.
        [CloudCodeFunction("DesbloquearNodo")]
        public async Task<ResultadoNodo> DesbloquearNodo(IExecutionContext context, IGameApiClient api, string idNodo)
        {
            var cfg = await LeerConfiguracionAsync(context, api);
            var estado = await LeerEstadoAsync(context, api, context.PlayerId);
            var nodo = cfg.Nodos.FirstOrDefault(n => n.Id == idNodo);

            var motivo = ReglasProgresion.PorQueNoSePuede(nodo, estado.Progreso.Desbloqueos, estado.Perfil.NivelCuenta, estado.Saldos.Monedas);
            if (motivo != null)
                return new ResultadoNodo { Exito = false, Mensaje = motivo, MonedaRestante = estado.Saldos.Monedas, Desbloqueos = estado.Progreso.Desbloqueos };

            estado.Saldos.Monedas -= nodo.CuestaMoneda;
            estado.Progreso.Desbloqueos.Add(nodo.Id);
            await EscribirEstadoBatchAsync(context, api, estado);

            return new ResultadoNodo
            {
                Exito = true, Mensaje = $"Desbloqueado {nodo.Id} por {nodo.CuestaMoneda} oro.",
                Oro = nodo.CuestaMoneda, MonedaRestante = estado.Saldos.Monedas, Desbloqueos = estado.Progreso.Desbloqueos
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

        // ---------- LAB 10: helpers de integracion ----------
        static List<HeroeEstado> ArmarPropios(InventarioEstado inv, List<string> equipo)
        {
            var lista = new List<HeroeEstado>();
            foreach (var id in equipo ?? new List<string>())
            {
                var h = inv?.Heroes?.FirstOrDefault(x => x.Id == id);
                if (h != null)
                    lista.Add(new HeroeEstado { Id = h.Id, Vida = h.Vida, Ataque = h.Ataque, Nivel = h.Nivel, Experiencia = h.Experiencia });
                else if (TablaRivales.Heroes.TryGetValue(id, out var s))
                    lista.Add(new HeroeEstado { Id = id, Vida = s.vida, Ataque = s.ataque });
                else
                    lista.Add(new HeroeEstado { Id = id });
            }
            return lista;
        }

        static List<HeroeEstado> ArmarRivales(List<string> declarados, List<HeroeInv> inventario)
        {
            var lista = new List<HeroeEstado>();
            foreach (var id in declarados ?? new List<string>())
            {
                var inv = inventario?.FirstOrDefault(h => h.id == id);
                if (inv != null) lista.Add(new HeroeEstado { Id = id, Vida = inv.vida, Ataque = inv.ataque });
                else if (TablaRivales.Heroes.TryGetValue(id, out var s)) lista.Add(new HeroeEstado { Id = id, Vida = s.vida, Ataque = s.ataque });
            }
            return lista;
        }

        static void AplicarInventario(InventarioEstado inv, List<HeroeEstado> propios)
        {
            if (inv == null) return;
            foreach (var p in propios)
            {
                var h = inv.Heroes.FirstOrDefault(x => x.Id == p.Id);
                if (h != null) { h.Nivel = p.Nivel; h.Experiencia = p.Experiencia; }
            }
        }

        static ResultadoCombate RespuestaRepetida(EstadoJugador estado)
        {
            var uc = estado.UltimoCombate;
            return new ResultadoCombate
            {
                Gano = uc.Victoria, RivalNombre = "(repetido)", Fuente = "repetido",
                Oro = 0, Trofeos = 0, Xp = uc.Experiencia, Nivel = estado.Perfil.NivelCuenta,
                Subieron = uc.Subieron, Botin = uc.Botin, Repetida = true,
                Mensaje = "Este combate ya se habia resuelto: mismo resultado, sin volver a premiar."
            };
        }

        // Lee la llave "progresion" de Remote Config; si no esta o falla, usa los valores por defecto.
        static async Task<ConfigProgresion> LeerConfiguracionAsync(IExecutionContext context, IGameApiClient api)
        {
            try
            {
                var req = new SettingsDeliveryRequest(
                    context.ProjectId, context.PlayerId, null, null, null, null,
                    new List<string> { "progresion" }, null, false, null, null, null);
                var resp = await api.RemoteConfigSettings.AssignSettingsAsync(context, context.ServiceToken, req, default);
                var token = JToken.Parse(JsonConvert.SerializeObject(resp.Data?.Configs));
                var val = BuscarTokenProgresion(token);
                if (val != null)
                {
                    var cfg = val.ToObject<ConfigProgresion>();
                    if (cfg != null) return cfg;
                }
            }
            catch { /* Remote Config no disponible o formato inesperado: defaults */ }
            return new ConfigProgresion();
        }

        static JToken BuscarTokenProgresion(JToken t)
        {
            if (t is JObject o)
            {
                var dir = o["progresion"];
                if (dir != null) return dir["value"] ?? dir;
                if (o["experiencia"] != null || o["niveles"] != null) return o;
                foreach (var prop in o.Properties())
                {
                    var r = BuscarTokenProgresion(prop.Value);
                    if (r != null) return r;
                }
            }
            else if (t is JArray a)
            {
                foreach (var it in a)
                {
                    var r = BuscarTokenProgresion(it);
                    if (r != null) return r;
                }
            }
            return null;
        }

        static async Task<EstadoJugador> LeerEstadoAsync(IExecutionContext context, IGameApiClient api, string playerId)
        {
            var estado = new EstadoJugador();
            var keys = new List<string>
            {
                Llaves.Inventario, LlavesProgresion.Operaciones, LlavesProgresion.Perfil,
                LlavesProgresion.Progreso, LlavesProgresion.Saldos, LlavesProgresion.UltimoCombate
            };
            var resp = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId, keys, null, default);
            var items = resp.Data?.Results;
            if (items == null) return estado;
            foreach (var item in items)
            {
                if (item.Value == null) continue;
                var json = JsonConvert.SerializeObject(item.Value);
                switch (item.Key)
                {
                    case Llaves.Inventario: estado.Inventario = JsonConvert.DeserializeObject<InventarioEstado>(json) ?? estado.Inventario; break;
                    case LlavesProgresion.Operaciones: estado.Operaciones = JsonConvert.DeserializeObject<OperacionesEstado>(json) ?? estado.Operaciones; break;
                    case LlavesProgresion.Perfil: estado.Perfil = JsonConvert.DeserializeObject<PerfilEstado>(json) ?? estado.Perfil; break;
                    case LlavesProgresion.Progreso: estado.Progreso = JsonConvert.DeserializeObject<ProgresoEstado>(json) ?? estado.Progreso; break;
                    case LlavesProgresion.Saldos: estado.Saldos = JsonConvert.DeserializeObject<SaldosEstado>(json) ?? estado.Saldos; break;
                    case LlavesProgresion.UltimoCombate: estado.UltimoCombate = JsonConvert.DeserializeObject<UltimoCombateEstado>(json) ?? estado.UltimoCombate; break;
                }
            }
            return estado;
        }

        // Las 6 llaves protegidas se escriben en UN solo batch (cobro + progresion atomicos).
        static async Task EscribirEstadoBatchAsync(IExecutionContext context, IGameApiClient api, EstadoJugador estado)
        {
            var items = new List<SetItemBody>
            {
                new SetItemBody(Llaves.Inventario, estado.Inventario, null),
                new SetItemBody(LlavesProgresion.Operaciones, estado.Operaciones, null),
                new SetItemBody(LlavesProgresion.Perfil, estado.Perfil, null),
                new SetItemBody(LlavesProgresion.Progreso, estado.Progreso, null),
                new SetItemBody(LlavesProgresion.Saldos, estado.Saldos, null),
                new SetItemBody(LlavesProgresion.UltimoCombate, estado.UltimoCombate, null),
            };
            await api.CloudSaveData.SetProtectedItemBatchAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId,
                new SetItemBatchBody(items), default);
        }
    }
}
