// ============ LAB01: ARRANQUE DE SERVICIOS Y AUTENTICACIÓN (UGS) ============
// Pasos de la guía que cubre:
//  - Paso 5: arranque de UnityServices (InitializeAsync + entorno) + log del estado
//  - Paso 6: los dos fallos que hay que documentar (Project ID inválido / sin internet)
//            se ejecutan con los botones 8 y 9 del panel, con el detalle exacto de la excepción
//  - Paso 7: handshake real con SignInAnonymouslyAsync (lo que valida el dashboard)
//  - Implementación pedida: invitado (anónimo), usuario/contraseña y unificar usuarios
// Montaje: escena "Lab01-Autenticacion" -> GameObject "AuthLab01" -> PanelAuthLab01.
// No hace falta ningún define extra: los paquetes están fijos en Packages/manifest.json.

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

public static class ServiciosBootstrap
{
    // ---- Paso 5: el bloque de arranque que pide la guía ----
    public const string EntornoProduccion = "production";

    static string entornoActual = EntornoProduccion;

    // El panel se suscribe para replicar en pantalla lo mismo que va a la consola.
    public static event Action<string> AlRegistrar;

    public static string Entorno { get { return entornoActual; } }

    public static bool Listo
    {
        get { return UnityServices.State == ServicesInitializationState.Initialized; }
    }

    // OJO: tocar AuthenticationService.Instance antes de InitializeAsync lanza ServicesInitializationException.
    static IAuthenticationService Auth { get { return AuthenticationService.Instance; } }

    // El SDK escribe el motivo real en la consola ("[Authentication]: Request failed: 401, {\"detail\":\"...\"}")
    // y después lanza una excepción SIN mensaje. Guardamos esa línea para poder mostrarla en pantalla.
    static string ultimoDetalleServicio = "";
    static bool escuchandoConsola;
    static readonly Regex RegexStatus = new Regex("Request failed: (\\d+)", RegexOptions.Compiled);
    static readonly Regex RegexDetail = new Regex("\"detail\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);

    public static string DetalleServicio { get { return ultimoDetalleServicio; } }

    public static void EscucharConsola()
    {
        if (escuchandoConsola) return;
        escuchandoConsola = true;

        Application.logMessageReceived += (mensaje, traza, tipo) =>
        {
            if (!string.IsNullOrEmpty(mensaje) && mensaje.StartsWith("[Authentication]"))
                ultimoDetalleServicio = mensaje;
        };
    }

    // ------------------------------------------------------------------
    // Paso 5 — arranque
    // ------------------------------------------------------------------
    public static async Task InicializarAsync(string entorno = EntornoProduccion)
    {
        entornoActual = entorno;
        EscucharConsola();
        ultimoDetalleServicio = "";

        // Si otro script ya disparó la inicialización, esperamos a que termine.
        while (UnityServices.State == ServicesInitializationState.Initializing)
            await Task.Delay(50);

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            Registrar($"Servicios ya estaban listos. Estado: {UnityServices.State}");
            return;
        }

        var opciones = new InitializationOptions();
        opciones.SetEnvironmentName(entorno);

        await UnityServices.InitializeAsync(opciones);
        Registrar($"Servicios listos. Estado: {UnityServices.State}");
    }

    // ------------------------------------------------------------------
    // Paso 7 — handshake: lo que crea la ficha en Player Management -> Players
    // ------------------------------------------------------------------
    public static async Task LoginAnonimoAsync()
    {
        await InicializarAsync(entornoActual);

        if (Auth.IsSignedIn)
        {
            Registrar($"Ya había sesión (perfil '{Auth.Profile}', id {Auth.PlayerId}).");
            return;
        }

        await Auth.SignInAnonymouslyAsync();
        Registrar($"Handshake OK (invitado). Player ID: {Auth.PlayerId} | perfil: '{Auth.Profile}' | autorizado: {Auth.IsAuthorized}");
        Registrar("Comprueba el dashboard: Player Management -> Players -> debe estar esta misma ficha.");
    }

    // ------------------------------------------------------------------
    // Implementación pedida 1: usuario y contraseña
    // ------------------------------------------------------------------
    public static async Task CrearCuentaAsync(string usuario, string clave)
    {
        ValidarCredenciales(usuario, clave);
        await InicializarAsync(entornoActual);

        // SignUp/SignIn exigen no tener sesión abierta (ErrorCode 10000 ClientInvalidUserState).
        if (Auth.IsSignedIn)
        {
            Registrar($"Cierro la sesión actual ({Auth.PlayerId}) para poder crear la cuenta nueva.");
            Auth.SignOut();
            await Task.Delay(150);
        }

        await Auth.SignUpWithUsernamePasswordAsync(usuario, clave);
        Registrar($"Cuenta creada: usuario '{usuario}' | Player ID: {Auth.PlayerId}");
    }

    public static async Task IniciarSesionAsync(string usuario, string clave)
    {
        ValidarCredenciales(usuario, clave);
        await InicializarAsync(entornoActual);

        if (Auth.IsSignedIn)
        {
            Auth.SignOut();
            await Task.Delay(150);
        }

        await Auth.SignInWithUsernamePasswordAsync(usuario, clave);
        var ficha = await Auth.GetPlayerInfoAsync();
        Registrar($"Sesión con usuario/contraseña: '{usuario}' | Player ID: {ficha.Id} | identidades: [{Identidades(ficha)}]");
    }

    // ------------------------------------------------------------------
    // Implementación pedida 2: unificar usuarios
    // El invitado recibe credenciales de usuario/contraseña SIN cambiar de Player ID,
    // así la partida del invitado no se pierde (AddUsernamePasswordAsync).
    // ------------------------------------------------------------------
    public static async Task UnificarConUsuarioAsync(string usuario, string clave)
    {
        ValidarCredenciales(usuario, clave);
        await InicializarAsync(entornoActual);

        if (!Auth.IsSignedIn)
            throw new InvalidOperationException("Necesitás una sesión activa: primero entrá como invitado.");

        string idAntes = Auth.PlayerId;
        string identidadesAntes = Identidades(Auth.PlayerInfo);

        await Auth.AddUsernamePasswordAsync(usuario, clave);
        var ficha = await Auth.GetPlayerInfoAsync();

        Registrar($"Unificado: '{usuario}' quedó añadido al jugador {idAntes}. " +
                  $"Identidades [{identidadesAntes}] -> [{Identidades(ficha)}]");
        Registrar(ficha.Id == idAntes
            ? "Player ID IDÉNTICO antes y después: la partida del invitado se conserva."
            : $"¡OJO! El Player ID cambió ({idAntes} -> {ficha.Id}): no se unificó, se creó otro jugador.");
    }

    // ------------------------------------------------------------------
    // Soporte del panel
    // ------------------------------------------------------------------
    public static async Task FichaAsync()
    {
        await InicializarAsync(entornoActual);

        if (!Auth.IsSignedIn)
        {
            Registrar("Sin sesión: no hay ficha que consultar.");
            return;
        }

        var ficha = await Auth.GetPlayerInfoAsync();
        Registrar($"Ficha: id={ficha.Id} | creado={Fecha(ficha.CreatedAt)} | " +
                  $"username={(string.IsNullOrEmpty(ficha.Username) ? "(ninguno)" : ficha.Username)} | " +
                  $"identidades=[{Identidades(ficha)}] | clave actualizada={Fecha(ficha.LastPasswordUpdate)}");
    }

    public static void CerrarSesion()
    {
        if (!Listo)
        {
            Registrar("Servicios sin inicializar: no hay sesión que cerrar.");
            return;
        }

        if (!Auth.IsSignedIn)
        {
            Registrar("Ya estabas sin sesión.");
            return;
        }

        string id = Auth.PlayerId;
        // SignOut() conserva las credenciales del perfil: al volver a entrar anónimo aparece el MISMO jugador.
        Auth.SignOut();
        Registrar($"Sesión cerrada ({id}). El perfil '{Auth.Profile}' conserva sus credenciales.");
    }

    public static string ResumenEstado()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Editor      : {Application.unityVersion}  |  core {VersionDe(typeof(UnityServices))}  |  authentication {VersionDe(typeof(AuthenticationService))}");
        sb.AppendLine($"Estado      : {UnityServices.State}  |  entorno: {entornoActual}");
        sb.AppendLine($"Project ID  : {Application.cloudProjectId}");

        if (!Listo)
        {
            sb.AppendLine("Sesión      : (inicializa los servicios para ver la sesión)");
            return sb.ToString().TrimEnd();
        }

        var auth = Auth;
        string usuario = auth.PlayerInfo == null ? null : auth.PlayerInfo.Username;

        sb.AppendLine($"Sesión      : {(auth.IsSignedIn ? (string.IsNullOrEmpty(usuario) ? "invitado (anónimo)" : $"usuario '{usuario}'") : "sin sesión")}  |  perfil: '{auth.Profile}'");
        sb.AppendLine($"Player ID   : {(string.IsNullOrEmpty(auth.PlayerId) ? "-" : auth.PlayerId)}");
        sb.AppendLine($"Ficha       : username={(string.IsNullOrEmpty(usuario) ? "(ninguno)" : usuario)}  |  credenciales guardadas: {auth.SessionTokenExists}");
        sb.AppendLine($"Identidades : {Identidades(auth.PlayerInfo)}");
        return sb.ToString().TrimEnd();
    }

    // ------------------------------------------------------------------
    // Consultas que usa el panel (menú tipo juego)
    // ------------------------------------------------------------------
    public static bool HaySesion
    {
        get { return Listo && Auth.IsSignedIn; }
    }

    public static string UsuarioActual
    {
        get
        {
            if (!HaySesion) return null;
            var ficha = Auth.PlayerInfo;
            return ficha == null ? null : ficha.Username;
        }
    }

    // Invitado = hay sesión pero sin usuario/contraseña asociado.
    public static bool EsInvitado
    {
        get { return HaySesion && string.IsNullOrEmpty(UsuarioActual); }
    }

    public static string PlayerIdActual
    {
        get { return HaySesion ? Auth.PlayerId : null; }
    }

    // Resumen corto para la pantalla "Mi cuenta".
    public static string ResumenSesion()
    {
        if (!HaySesion) return "Todavía no hay ninguna sesión abierta.";

        var auth = Auth;
        string usuario = UsuarioActual;
        var sb = new StringBuilder();
        sb.AppendLine($"Player ID   : {auth.PlayerId}");
        sb.AppendLine($"Cuenta      : {(string.IsNullOrEmpty(usuario) ? "invitado (sin usuario)" : "usuario y contraseña")}  |  usuario: {(string.IsNullOrEmpty(usuario) ? "-" : usuario)}");
        sb.AppendLine($"Identidades : {Identidades(auth.PlayerInfo)}");
        sb.AppendLine($"Perfil      : '{auth.Profile}'  |  credenciales guardadas: {auth.SessionTokenExists}");
        return sb.ToString().TrimEnd();
    }

    // Dos líneas para el pie del menú de inicio.
    public static string ResumenServicios()
    {
        return $"Servicios: {UnityServices.State}  |  core {VersionDe(typeof(UnityServices))}  |  authentication {VersionDe(typeof(AuthenticationService))}\n" +
               $"Project ID: {Application.cloudProjectId}";
    }

    public static void ValidarCredenciales(string usuario, string clave)
    {
        // Reglas del SDK: usuario 3-20 de [A-Za-z0-9.-@_] · clave 8-30 con mayúscula, minúscula, número y símbolo.
        if (string.IsNullOrEmpty(usuario) || !Regex.IsMatch(usuario, @"^[A-Za-z0-9.\-@_]{3,20}$"))
            throw new ArgumentException("Usuario inválido: 3-20 caracteres alfanuméricos o . - @ _ (sin espacios).");

        if (!ClaveValida(clave))
            throw new ArgumentException("Contraseña inválida: 8-30 caracteres con al menos 1 mayúscula, 1 minúscula, 1 número y 1 símbolo.");
    }

    static bool ClaveValida(string clave)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length < 8 || clave.Length > 30) return false;
        return clave.Any(char.IsUpper) && clave.Any(char.IsLower) &&
               clave.Any(char.IsDigit) && clave.Any(c => !char.IsLetterOrDigit(c));
    }

    // Traduce la línea del servicio a una instrucción concreta (lo que ve el jugador en pantalla).
    public static string ExplicacionServicio()
    {
        return ExplicarDetalle(ultimoDetalleServicio);
    }

    static string ExplicarDetalle(string linea)
    {
        if (string.IsNullOrEmpty(linea)) return null;

        var coincidenciaDetalle = RegexDetail.Match(linea);
        string detalle = coincidenciaDetalle.Success ? coincidenciaDetalle.Groups[1].Value : linea;
        var coincidenciaEstado = RegexStatus.Match(linea);
        int estado = coincidenciaEstado.Success ? int.Parse(coincidenciaEstado.Groups[1].Value) : 0;
        string texto = detalle.ToLowerInvariant();

        if (texto.Contains("provider is not available") || texto.Contains("provider_not_enabled"))
            return "El proveedor Username & password está DESHABILITADO en el proyecto (401). " +
                   "Dashboard: Player Management -> Player Authentication -> Identity providers -> habilitar Username and password.";

        if (texto.Contains("already exist") || estado == 409)
            return "Ese nombre de usuario ya existe. Elegí otro o entrá con YA TENGO CUENTA.";

        if (texto.Contains("password"))
            return "La contraseña no cumple las reglas del servicio (8-30 con mayúscula, minúscula, número y símbolo).";

        if (texto.Contains("username") || texto.Contains("invalid identifier"))
            return "El nombre de usuario no cumple las reglas (3-20: letras, números, . - @ _).";

        if (estado == 404 || texto.Contains("resource_not_found"))
            return "404 RESOURCE_NOT_FOUND: el Project ID no existe o el proyecto no está linkeado.";

        if (estado == 401 || estado == 403)
            return $"Sin permiso para esta operación ({(estado > 0 ? estado.ToString() : "401/403")}). Detalle: {detalle}";

        return $"El servicio respondió {(estado > 0 ? estado.ToString() : "un error")}: {detalle}";
    }

    // El mensaje del catch suele llegar VACÍO: el detalle real está en las líneas [Authentication] de la consola.
    public static string DetallarExcepcion(Exception e)
    {
        if (e == null) return "(sin excepción)";

        var sb = new StringBuilder();
        var rfe = e as RequestFailedException;
        sb.AppendLine($"[{e.GetType().Name}] ErrorCode={(rfe != null ? rfe.ErrorCode.ToString() : "n/a")}");
        sb.AppendLine(string.IsNullOrEmpty(e.Message)
            ? "(mensaje VACÍO: el detalle está en las líneas [Authentication] de la consola)"
            : e.Message);

        for (var interior = e.InnerException; interior != null; interior = interior.InnerException)
        {
            sb.AppendLine($"  <-- [{interior.GetType().Name}] " +
                          (string.IsNullOrEmpty(interior.Message) ? "(mensaje vacío)" : interior.Message));
        }

        // Caso típico del paso 6: 401 con "provider is not available". El SDK lo resume en 10002
        // y borra el mensaje, así que la causa solo se ve en la línea [Authentication].
        if (!string.IsNullOrEmpty(ultimoDetalleServicio))
        {
            sb.AppendLine("Detalle del servicio: " + ultimoDetalleServicio);
            string explicacion = ExplicarDetalle(ultimoDetalleServicio);
            if (!string.IsNullOrEmpty(explicacion)) sb.AppendLine("=> " + explicacion);
        }
        else if (rfe != null && rfe.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
        {
            sb.AppendLine("Pista: si la línea [Authentication] dice \"provider is not available: PERMISSION_DENIED\", " +
                          "falta habilitar el proveedor Username & password en el dashboard " +
                          "(Player Management -> Identity providers).");
        }

        return sb.ToString().TrimEnd();
    }

    static string Identidades(PlayerInfo ficha)
    {
        if (ficha == null || ficha.Identities == null || ficha.Identities.Count == 0)
            return "(sin ficha cargada)";
        return string.Join(", ", ficha.Identities.Select(i => i.TypeId));
    }

    static string Fecha(DateTime? valor)
    {
        return valor.HasValue ? valor.Value.ToString("yyyy-MM-dd HH:mm:ss") + "Z" : "-";
    }

    static string VersionDe(Type tipo)
    {
#if UNITY_EDITOR
        var paquete = UnityEditor.PackageManager.PackageInfo.FindForAssembly(tipo.Assembly);
        if (paquete != null) return paquete.version;
#endif
        return tipo.Assembly.GetName().Version.ToString();
    }

    static void Registrar(string mensaje)
    {
        string linea = "[DVSC] " + mensaje;
        Debug.Log(linea);
        var manejador = AlRegistrar;
        if (manejador != null) manejador(linea);
    }
}
