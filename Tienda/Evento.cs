using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.RemoteConfig.Model;

// ============ LAB 12: LIVE OPS — EL EVENTO PROGRAMADO ============
// Un evento NO es un estado del jugador ni una regla de validación: es un cambio de
// NÚMEROS que el SERVIDOR aplica durante una ventana de tiempo. Por eso vive entero en
// Remote Config (llave "campana_evento") y se enciende/apaga desde el dashboard con un
// Game Override, sin publicar una versión nueva del juego.
//
// Tres reglas que no se negocian:
//   1. El cliente NUNCA manda el evento ni el resultado: pregunta (VistaEvento) y pinta.
//   2. La ventana se evalúa en UTC contra los datos del propio JSON; el reloj del
//      dispositivo no participa (el jugador podría atrasarlo).
//   3. Todo lo de este archivo es una REGLA PURA salvo LiveOps.LeerAsync: se prueba con
//      `dotnet test` sin nube.
//
// Si la llave NO está publicada, ConfigEvento() queda APAGADO (activo=false, factor 1.0):
// el módulo sigue funcionando igual que en el Lab 11.

namespace Tienda
{
    // ---------- Configuración: llave "campana_evento" ----------
    // El MISMO JSON se publica dos veces:
    //   · base (S12-campana-evento.rc):  { "activo": false }  -> lo que ve todo el mundo
    //   · override (Game Overrides):     el evento encendido con su ventana en UTC
    public class ConfigEvento
    {
        [JsonProperty("activo")] public bool Activo { get; set; }
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("nombre")] public string Nombre { get; set; } = "";
        // Ventana en UTC (ISO 8601). Vacías = manda solo "activo" (lo apaga el dashboard).
        [JsonProperty("inicioUtc")] public string InicioUtc { get; set; } = "";
        [JsonProperty("finUtc")] public string FinUtc { get; set; } = "";
        [JsonProperty("multiplicadorDano")] public double MultiplicadorDano { get; set; } = 1.0;
        // Fracción: 0.3 = -30 %. Nunca se resta más del 100 %.
        [JsonProperty("descuentoTienda")] public double DescuentoTienda { get; set; }
        // Qué ítems del catálogo llevan el descuento (no se duplica el catálogo).
        [JsonProperty("heroesEspeciales")] public List<string> HeroesEspeciales { get; set; } = new List<string>();
        // Ítems EXTRA del botín, con el mismo formato que la tabla del S10 (id, tipo, cantidad, peso).
        [JsonProperty("botinExclusivo")] public List<PremioBotin> BotinExclusivo { get; set; } = new List<PremioBotin>();
    }

    // ---------- Configuración: llave "bonus_bienvenida" ----------
    // La cantidad NO viaja en el cliente: la decide el override del segmento «nuevo».
    public class ConfigBienvenida
    {
        [JsonProperty("moneda")] public string Moneda { get; set; } = "ORO";
        [JsonProperty("cantidad")] public int Cantidad { get; set; }
    }

    // Las dos llaves se leen en UNA sola petición de Remote Config.
    public class ConfigLiveOps
    {
        public ConfigEvento Evento { get; set; } = new ConfigEvento();
        public ConfigBienvenida Bienvenida { get; set; } = new ConfigBienvenida();

        // Si Remote Config falla, acá queda el motivo. Sin esto, el catch vacío
        // devolvía los valores por defecto y parecía que el override no aplicaba.
        public string Error { get; set; } = "";
    }

    // ---------- Lo que devuelve VistaEvento (paso 6/7) ----------
    public class ResultadoVistaEvento
    {
        public bool Activo { get; set; }
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string InicioUtc { get; set; } = "";
        public string FinUtc { get; set; } = "";
        public string AhoraUtc { get; set; } = "";          // hora del SERVIDOR
        public long SegundosRestantes { get; set; }         // 0 = apagado
        public double FactorDano { get; set; } = 1.0;
        public double Descuento { get; set; }
        public List<string> HeroesEspeciales { get; set; } = new List<string>();
        public List<PremioBotin> BotinExclusivo { get; set; } = new List<PremioBotin>();
        // Segmento de bienvenida (paso 8): lo calcula el SERVIDOR (nivel de cuenta <= 1).
        public bool EsNuevo { get; set; }
        public string MonedaBono { get; set; } = "ORO";
        public int BonoBienvenida { get; set; }
        public bool BonoReclamado { get; set; }
        // El reloj que se usó: siempre el del servidor. La consola del cliente muestra el local.
        public string FuenteReloj { get; set; } = "servidor";
        public string Mensaje { get; set; } = "";
    }

    // ---------- Lo que devuelve ReclamarBonusBienvenida (paso 8) ----------
    public class ResultadoBono
    {
        public bool Exito { get; set; }
        public bool YaReclamado { get; set; }
        public string Moneda { get; set; } = "ORO";
        public int Cantidad { get; set; }
        public int Saldo { get; set; }
        public string Mensaje { get; set; } = "";
    }

    // ---------- Lo que devuelve ComprarHeroe (paso 3) ----------
    public class ResultadoCompra
    {
        public bool Exito { get; set; }
        public string Heroe { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int PrecioBase { get; set; }
        public long Precio { get; set; }
        public double Descuento { get; set; }
        public bool DescuentoAplicado { get; set; }
        public int Saldo { get; set; }
        public string Mensaje { get; set; } = "";
    }

    // ---------- Catálogo de la tienda (S06/S07) ----------
    // El precio NO se duplica en el evento: heroesEspeciales solo LISTA ids de este
    // catálogo. Las stats salen de TablaRivales (la misma tabla de siempre).
    public class ItemCatalogo
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int Precio { get; set; }
    }

    public static class Catalogo
    {
        public static readonly Dictionary<string, ItemCatalogo> Heroes = new Dictionary<string, ItemCatalogo>
        {
            { "mago",              new ItemCatalogo { Id = "mago",              Nombre = "Mago",              Precio = 1200 } },
            { "vampiro",           new ItemCatalogo { Id = "vampiro",           Nombre = "Vampiro",           Precio = 900  } },
            { "arquero",           new ItemCatalogo { Id = "arquero",           Nombre = "Arquero",           Precio = 700  } },
            { "escudero",          new ItemCatalogo { Id = "escudero",          Nombre = "Escudero",          Precio = 500  } },
            { "guerrero_del_hacha",new ItemCatalogo { Id = "guerrero_del_hacha",Nombre = "Guerrero del hacha",Precio = 1500 } },
            { "draconiano",        new ItemCatalogo { Id = "draconiano",        Nombre = "Draconiano",        Precio = 1800 } },
        };

        public static ItemCatalogo Buscar(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Heroes.TryGetValue(id, out var item) ? item : null;
        }
    }

    // ---------- REGLAS PURAS DEL EVENTO ----------
    public static class ReglasEvento
    {
        // ISO 8601 -> UTC. Vacío o basura devuelve null (la ventana es opcional).
        public static DateTime? Fecha(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            if (DateTime.TryParse(texto, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fecha))
                return fecha;
            return null;
        }

        // PASO 2: ¿el evento está encendido AHORA? Manda "activo" y, si hay fechas, la
        // ventana se evalúa en UTC: [inicio, fin). Un evento sin fecha de fin lo apaga el
        // dashboard (el override deja de aplicar), no el paso del tiempo.
        public static bool Activo(ConfigEvento cfg, DateTime utcNow)
        {
            if (cfg == null || !cfg.Activo) return false;
            var inicio = Fecha(cfg.InicioUtc);
            var fin = Fecha(cfg.FinUtc);
            if (inicio.HasValue && utcNow < inicio.Value) return false;
            if (fin.HasValue && utcNow >= fin.Value) return false;
            return true;
        }

        // PASO 7: cuenta atrás que puede pintar el cliente. Sale del fin del JSON y de la
        // hora del SERVIDOR; nunca del reloj del dispositivo.
        public static long SegundosRestantes(ConfigEvento cfg, DateTime utcNow)
        {
            if (!Activo(cfg, utcNow)) return 0;
            var fin = Fecha(cfg.FinUtc);
            if (!fin.HasValue) return 0;
            return Math.Max(0, (long)(fin.Value - utcNow).TotalSeconds);
        }

        // PASO 2: el daño escala el ataque del equipo PROPIO antes de simular.
        // Sin evento (o con multiplicador <= 0) devuelve 1.0: se juega igual que en el S10.
        public static double FactorDano(ConfigEvento cfg, DateTime utcNow)
        {
            if (!Activo(cfg, utcNow)) return 1.0;
            return cfg.MultiplicadorDano > 0 ? cfg.MultiplicadorDano : 1.0;
        }

        // 12 de ataque × 1.25 = 15 (redondeo del servidor, no del cliente).
        public static int AtaqueEscalado(int ataque, double factor)
        {
            var escalado = (int)Math.Round(ataque * factor);
            return escalado < 1 ? 1 : escalado;
        }

        // PASO 3: el descuento lo decide el SERVIDOR al cobrar. Solo aplica a los héroes
        // que el evento LISTA y solo con el evento activo; el tope es -100 %.
        public static long PrecioConDescuento(ConfigEvento cfg, string item, long precioBase, DateTime utcNow)
        {
            if (precioBase <= 0) return 0;
            if (!Activo(cfg, utcNow) || cfg.DescuentoTienda <= 0) return precioBase;
            if (cfg.HeroesEspeciales == null || !cfg.HeroesEspeciales.Contains(item)) return precioBase;
            var fraccion = Math.Min(1.0, cfg.DescuentoTienda);
            return (long)Math.Round(precioBase * (1.0 - fraccion));
        }

        // PASO 4: la tabla del evento se SUMA a la del S10, no la reemplaza. Se devuelve
        // una COPIA: la configuración base del jugador no se toca.
        public static ConfigBotin BotinConEvento(ConfigEvento cfg, ConfigBotin botin, DateTime utcNow)
        {
            var copia = new ConfigBotin
            {
                Tiradas = botin != null && botin.Tiradas > 0 ? botin.Tiradas : 1,
                Premios = new List<PremioBotin>()
            };
            if (botin?.Premios != null)
                foreach (var premio in botin.Premios)
                    copia.Premios.Add(new PremioBotin
                    {
                        Id = premio.Id, Tipo = premio.Tipo, Cantidad = premio.Cantidad, Peso = premio.Peso
                    });

            if (Activo(cfg, utcNow) && cfg.BotinExclusivo != null)
                foreach (var premio in cfg.BotinExclusivo)
                    if (premio != null && !string.IsNullOrEmpty(premio.Id))
                        copia.Premios.Add(new PremioBotin
                        {
                            Id = premio.Id, Tipo = premio.Tipo, Cantidad = premio.Cantidad, Peso = premio.Peso
                        });
            return copia;
        }

        // PASO 8: el segmento «nuevo» lo calcula el SERVIDOR con el perfil PROTEGIDO.
        // El cliente no manda nada: no puede decir que es nuevo para cobrar dos veces.
        public static bool EsNuevo(int nivelCuenta) { return nivelCuenta <= 1; }

        // El bonus se entrega UNA vez: manda la bandera del perfil protegido.
        public static int BonoDeBienvenida(ConfigBienvenida cfg, bool esNuevo, bool yaReclamado)
        {
            if (cfg == null || !esNuevo || yaReclamado || cfg.Cantidad <= 0) return 0;
            return cfg.Cantidad;
        }

        // hh:mm:ss para la consola y el informe (la UI del cliente tiene el suyo).
        public static string CuentaAtras(long segundos)
        {
            var s = Math.Max(0, segundos);
            return $"{s / 3600:00}:{(s % 3600) / 60:00}:{s % 60:00}";
        }
    }

    // ---------- Lectura de Remote Config (única parte que toca la red) ----------
    public static class LiveOps
    {
        // Las DOS llaves en una petición. El atributo "esNuevo" viaja en la petición
        // (POST AssignSettingsAsync) para que el override del segmento lo pueda usar;
        // NO se escribe en el estado del jugador ni lo manda el cliente.
        public static async Task<ConfigLiveOps> LeerAsync(
            IExecutionContext context, IGameApiClient api, bool esNuevo)
        {
            var cfg = new ConfigLiveOps();
            var peticion = new SettingsDeliveryRequest(
                projectId: context.ProjectId,
                userId: context.PlayerId,
                environmentId: string.IsNullOrEmpty(context.EnvironmentId) ? null : context.EnvironmentId,
                key: new List<string> { "campana_evento", "bonus_bienvenida" },
                attributes: Atributos(esNuevo));

            // El endpoint de entrega de Remote Config es el MISMO que consulta el SDK del
            // cliente, o sea que autentica con el token del JUGADOR. Con el ServiceToken
            // (service-to-service) responde 401 Unauthorized aunque el cuerpo sea correcto.
            var credenciales = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("jugador", context.AccessToken),
                new KeyValuePair<string, string>("servicio", context.ServiceToken)
            };

            var errores = new List<string>();
            foreach (var credencial in credenciales)
            {
                if (string.IsNullOrEmpty(credencial.Value)) continue;
                try
                {
                    var resp = await api.RemoteConfigSettings.AssignSettingsAsync(
                        context, credencial.Value, peticion, default);
                    LeerRespuesta(resp, cfg);
                    return cfg;   // respuesta valida: no hace falta la otra credencial
                }
                catch (Exception ex)
                {
                    errores.Add($"token de {credencial.Key} -> {Describir(ex, context)}");
                }
            }

            // Ninguna credencial sirvio: el motivo queda a la vista (VistaEvento lo publica).
            cfg.Error = errores.Count > 0
                ? string.Join(" | ", errores)
                : "sin credenciales para leer Remote Config";
            return cfg;
        }

        // El servidor EXIGE las tres ramas: si "unity" o "app" viajan en null responde
        // 400 "Invalid request value at path: ...RequestAttributes[\"unity\"]".
        // El modulo corre en la NUBE: los datos de "unity" son del servicio, no del telefono.
        static RequestAttributes Atributos(bool esNuevo)
        {
            return new RequestAttributes(
                unity: new UnityAttributes(
                    appVersion: "1.0.0",
                    appBuildVersion: "1",
                    country: "XX",
                    cpu: "cloud-code",
                    cpuFrequency: "0",
                    graphicsDeviceVendor: "cloud-code",
                    language: "es",
                    osVersion: "cloud-code",
                    platform: "cloud-code",
                    ram: 0,
                    model: "cloud-code",
                    timeSinceStart: 0),
                app: new Dictionary<string, string>(),
                user: new Dictionary<string, string> { { "esNuevo", esNuevo ? "true" : "false" } });
        }

        // Las DOS llaves vienen en la misma respuesta; BuscarToken las encuentra sin
        // depender de la forma exacta del documento.
        static void LeerRespuesta(
            Unity.Services.CloudCode.Shared.ApiResponse<SettingsDeliveryResponse> resp, ConfigLiveOps cfg)
        {
            var token = JToken.Parse(JsonConvert.SerializeObject(resp.Data?.Configs));

            var evento = Funciones.BuscarToken(token, "campana_evento");
            if (evento != null)
            {
                var leida = evento.ToObject<ConfigEvento>();
                if (leida != null) cfg.Evento = leida;
            }

            var bono = Funciones.BuscarToken(token, "bonus_bienvenida");
            if (bono != null)
            {
                var leida = bono.ToObject<ConfigBienvenida>();
                if (leida != null) cfg.Bienvenida = leida;
            }
        }

        // El cuerpo del error del servidor viene en la respuesta HTTP, no en el ToString
        // de la excepcion: sin esto solo se veia "Bad Request" / "Unauthorized".
        static string Describir(Exception ex, IExecutionContext context)
        {
            string cuerpo = "";
            try
            {
                var fallo = ex as Unity.Services.CloudCode.Shared.ApiException;
                if (fallo != null && fallo.Response != null)
                    cuerpo = fallo.Response.ErrorText ?? fallo.Response.RawContent ?? "";
            }
            catch { cuerpo = ""; }
            cuerpo = (cuerpo ?? "").Replace("\r", " ").Replace("\n", " ");
            if (cuerpo.Length > 240) cuerpo = cuerpo.Substring(0, 240);
            return $"{ex.GetType().Name}: {ex.Message}" +
                   (cuerpo.Length > 0 ? " >> " + cuerpo : "") +
                   $" (env={context.EnvironmentName}, player={context.PlayerId})";
        }
    }
}
