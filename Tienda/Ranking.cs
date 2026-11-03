using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Leaderboards.Model;
using Unity.Services.RemoteConfig.Model;

// ============ LAB 11: TABLA DE CLASIFICACIÓN (trofeos) ============
// El servicio de Leaderboards ya sabe ordenar, paginar, marcar tiers y cerrar
// temporadas: aquí solo se integra. Tres reglas que no se negocian:
//   1. QUIÉN escribe: el token del SERVICIO (no el del jugador). El cliente solo lee.
//   2. QUÉ se escribe: el TOTAL de trofeos (la tabla es KeepLatest), y solo DESPUÉS de
//      que el combate ya quedó guardado en Cloud Save.
//   3. DE DÓNDE sale el cierre: de la tabla (versiones archivadas); si el servicio no
//      las devuelve, de Remote Config ("temporada" es la red de seguridad).
namespace Tienda
{
    // ---------- Configuración: llave "temporada" de Remote Config ----------
    public class ConfigTemporada
    {
        [JsonProperty("tabla")] public string Tabla { get; set; } = Ranking.Tabla;
        [JsonProperty("cierre")] public string Cierre { get; set; } = "2026-12-01T05:00:00Z";
        // Defaults que replican S11-temporada.rc (si la RC no está publicada).
        [JsonProperty("premios")] public Dictionary<string, PremioTemporada> Premios { get; set; } =
            new Dictionary<string, PremioTemporada>
            {
                { "Oro",    new PremioTemporada { Nombre = "Corona de oro",     Gemas = 20 } },
                { "Plata",  new PremioTemporada { Nombre = "Corona de plata",   Gemas = 10 } },
                { "Bronce", new PremioTemporada { Nombre = "Corona de bronce",  Gemas = 5 } },
            };
    }

    public class PremioTemporada
    {
        [JsonProperty("nombre")] public string Nombre { get; set; } = "";
        [JsonProperty("gemas")] public int Gemas { get; set; }
    }

    // Lo que ve el cliente en la vista de temporada (paso 9).
    public class ResultadoTemporada
    {
        public string Tabla { get; set; } = "";
        public int Trofeos { get; set; }
        public int Puesto { get; set; }                 // 1..N · 0 = aún sin puntaje
        public string Tier { get; set; } = "";          // Oro · Plata · Bronce
        public string CierreUtc { get; set; } = "";
        public long SegundosRestantes { get; set; }
        public PremioTemporada Premio { get; set; } = new PremioTemporada();
        public string FuenteCierre { get; set; } = "";  // "tabla" o "rc"
    }

    // ---------- Integración con el servicio ----------
    public static class Ranking
    {
        // El nombre del archivo .lb es el id de la tabla.
        public const string Tabla = "trofeos_dvsc";
        const string BaseUrl = "https://leaderboards.services.api.unity.com/v1";

        // PASO 3: publicar el TOTAL con el token del SERVICIO.
        // Devuelve el puesto 1..N, o 0 si no se pudo publicar (el combate ya está guardado:
        // el siguiente combate corrige la tabla, porque se manda el total y no la diferencia).
        public static async Task<int> PublicarTrofeosAsync(
            IGameApiClient api, IExecutionContext context, string playerId, int trofeos)
        {
            if (string.IsNullOrEmpty(context?.ServiceToken) || string.IsNullOrEmpty(playerId)) return 0;
            try
            {
                var resp = await api.Leaderboards.AddLeaderboardPlayerScoreAsync(
                    context,
                    context.ServiceToken,                    // token del servicio, NO el del jugador
                    Guid.Parse(context.ProjectId),
                    Tabla,
                    playerId,
                    new AddLeaderboardScore(trofeos, null, null),  // total, sin metadata, versión viva
                    default);
                return resp.Data.Rank + 1;                   // el servicio cuenta desde 0
            }
            catch
            {
                return 0; // la tabla es un adorno: nunca debe tumbar el combate
            }
        }

        // Entrada del jugador (puesto y tier). null si aún no tiene puntaje (404).
        public static async Task<LeaderboardEntryWithUpdatedTime> LeerEntradaAsync(
            IGameApiClient api, IExecutionContext context, string playerId)
        {
            try
            {
                var resp = await api.Leaderboards.GetLeaderboardPlayerScoreAsync(
                    context, context.ServiceToken, Guid.Parse(context.ProjectId),
                    Tabla, playerId, null, default);
                return resp?.Data;
            }
            catch
            {
                return null;
            }
        }

        // PASO 9: ¿cuándo cierra la temporada? Primero la VERDAD de la tabla (versiones
        // archivadas); si el servicio no las devuelve, la fecha de Remote Config.
        // Se lee con HttpClient y JToken porque el modelo tipado exige "results" y el
        // servicio lo OMITE cuando la tabla todavía no ha archivado ninguna versión.
        public static async Task<(DateTime cierre, string fuente)> LeerCierreAsync(
            IExecutionContext context, ConfigTemporada cfg)
        {
            var respaldo = DateTime.TryParse(cfg?.Cierre, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal, out var deRc)
                ? deRc : DateTime.UtcNow;
            try
            {
                var url = $"{BaseUrl}/projects/{context.ProjectId}/leaderboards/{Tabla}/versions?limit=1";
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", context.ServiceToken);
                var texto = await http.GetStringAsync(url);
                var token = JToken.Parse(texto);

                var versiones = token["results"];
                var primera = versiones != null && versiones.Type == JTokenType.Array && versiones.HasValues
                    ? versiones[0] : null;
                var fin = primera?["end"];
                if (fin != null && DateTime.TryParse(fin.ToString(), null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal, out var cierre))
                    return (cierre, "tabla");
            }
            catch { /* sin versiones archivadas o servicio no disponible: manda la RC */ }

            return (respaldo, "rc");
        }

        // Lectura de la llave "temporada" con el token del jugador y red de seguridad.
        public static async Task<ConfigTemporada> LeerTemporadaAsync(
            IExecutionContext context, IGameApiClient api)
        {
            var cfg = new ConfigTemporada();
            try
            {
                var peticion = new SettingsDeliveryRequest(
                    context.ProjectId, context.PlayerId, null, null, null, null,
                    new List<string> { "temporada" }, null, false, null, null, null);
                var resp = await api.RemoteConfigSettings.AssignSettingsAsync(
                    context, context.ServiceToken, peticion, default);
                var token = JToken.Parse(JsonConvert.SerializeObject(resp.Data?.Configs));

                var nodo = token["temporada"] ?? token["configs"]?["temporada"];
                if (nodo == null && token.Type == JTokenType.Object)
                    foreach (var hijo in token.Children<JProperty>())
                        if (hijo.Name.IndexOf("temporada", StringComparison.OrdinalIgnoreCase) >= 0)
                        { nodo = hijo.Value; break; }

                if (nodo != null && nodo.Type == JTokenType.Object)
                {
                    var leida = nodo.ToObject<ConfigTemporada>();
                    if (leida != null)
                    {
                        if (!string.IsNullOrWhiteSpace(leida.Cierre)) cfg.Cierre = leida.Cierre;
                        if (leida.Premios != null && leida.Premios.Count > 0) cfg.Premios = leida.Premios;
                    }
                }
            }
            catch { /* RC no disponible: los defaults del código son los mismos */ }
            return cfg;
        }
    }
}
