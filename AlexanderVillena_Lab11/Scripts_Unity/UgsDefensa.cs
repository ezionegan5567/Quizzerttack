// ============ LAB09: CLIENTE CLOUD SAVE (requiere UGS_ON + paquetes) ============
// Pasos de la guía que cubre:
//  - Paso 1-2: guardar MI defensa con llave pública (SaveOptions/PublicWriteAccessClassOptions)
//  - Paso 2: leer la de OTRO con LoadOptions/PublicReadAccessClassOptions
//  - Paso 2: verificación 403 code 53 (petición cruda con tu token a datos ajenos)
//  - Paso 2: SwitchProfile/SignInAnonymously para probar sin dos computadoras

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;

public static class UgsDefensa
{
    // ---- Login anónimo robusto: espera estados "signing in/out" en curso ----
    static async Task AsegurarSesionAsync()
    {
        var auth = AuthenticationService.Instance;
        var t0 = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (auth.IsSignedIn) return;
            if (t0.Elapsed.TotalSeconds > 60)
                throw new Exception("La sesión anónima no se pudo completar en 60 s.");
            try
            {
                await auth.SignInAnonymouslyAsync();
                return;
            }
            catch (Exception e) when (e.Message != null && e.Message.Contains("already sign"))
            {
                await Task.Delay(200); // hay un intento en curso: esperar
            }
        }
    }

    // ---- Iniciar UGS + sign-in anónimo con perfil ----
    public static async Task IniciarAsync(string perfil = "principal")
    {
        // 1) Inicializar UGS ANTES de tocar los singletons
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();
        // si quedó "Initializing" por otra llamada, espera
        while (UnityServices.State == ServicesInitializationState.Initializing)
            await Task.Delay(50);

        // 2) Autenticación anónima con perfil (forzado, demo repetible)
        var auth = AuthenticationService.Instance;
        if (auth.Profile != perfil)
            auth.SwitchProfile(perfil);
        await AsegurarSesionAsync();
        Debug.Log($"[UGS] Conectado (perfil '{auth.Profile}', id {auth.PlayerId}).");
    }

    public static string MiPlayerId
    { get { return AuthenticationService.Instance.PlayerId; } }

    // ---- Paso 2: MI defensa (pública en escritura): dos llaves en una petición ----
    public static async Task PublicarDefensaAsync(DefensaPublica defensa)
    {
        defensa.ActualizadoUtc = DateTime.UtcNow.ToString("o");
        var datos = new Dictionary<string, object>
        {
            { Llaves.Defensa, defensa },
            { Llaves.Nivel, defensa.NivelCuenta }
        };
        var opciones = new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions());
        await CloudSaveService.Instance.Data.Player.SaveAsync(datos, opciones);
        Debug.Log($"[UGS] Defensa publicada: {defensa.Nombre} (nivel {defensa.NivelCuenta}, {defensa.Equipo.Count} héroes).");
    }

    // ---- Paso 2: la defensa de OTRO: misma llave, lectura pública con su playerId ----
    public static async Task<DefensaPublica> LeerDefensaDeAsync(string playerId)
    {
        var ajena = await CloudSaveService.Instance.Data.Player.LoadAsync(
            new HashSet<string> { Llaves.Defensa },
            new Unity.Services.CloudSave.Models.Data.Player.LoadOptions(new PublicReadAccessClassOptions(playerId)));
        if (ajena != null && ajena.TryGetValue(Llaves.Defensa, out var valor) && valor != null)
            return valor.Value.GetAs<DefensaPublica>();
        return null;
    }

    // ---- Paso 2: cambiar de jugador sin segunda computadora ----
    public static async Task CambiarPerfilAsync(string perfil)
    {
        var auth = AuthenticationService.Instance;
        if (auth.IsSignedIn) auth.SignOut(false); // false: conserva credenciales del perfil
        auth.SwitchProfile(perfil);               // SignOut(true) borra el token y crea uno nuevo
        await AsegurarSesionAsync();
        Debug.Log($"[UGS] Perfil activo: '{perfil}' (id {auth.PlayerId}).");
    }

    // ---- Paso 2 (verificación): escribir la defensa de otro con TU token → 403 code 53 ----
    // El SDK no expone escribir datos ajenos; la petición cruda es la que responde 403.
    // HttpClient (.NET): UnityWebRequest no resuelve este dominio en todos los casos.
    public static async Task VerificarEscrituraAjenaAsync(string otroPlayerId)
    {
        string projectId = Application.cloudProjectId;
        string url = $"https://cloud-save.services.api.unity.com/v1/data/projects/{projectId}/players/{otroPlayerId}/items";
        string body = "{\"key\":\"" + Llaves.Defensa + "\",\"value\":{\"Nombre\":\"hack\"}}";
        try
        {
            using var http = new System.Net.Http.HttpClient();
            using var contenido = new System.Net.Http.StringContent(body, Encoding.UTF8, "application/json");
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthenticationService.Instance.AccessToken);
            var resp = await http.PostAsync(url, contenido);
            string texto = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
                Debug.Log("[VERIFICACIÓN] ¡OJO! Se escribió ajena sin permiso (no debía). Respuesta: " + texto);
            else
                Debug.Log($"[VERIFICACIÓN] Escritura ajena rechazada: HTTP {(int)resp.StatusCode} | cuerpo={texto}");
        }
        catch (Exception e)
        {
            string detalle = e.Message;
            for (var ex = e.InnerException; ex != null; ex = ex.InnerException) detalle += " <-- " + ex.Message;
            Debug.Log("[VERIFICACIÓN] Error de conexión: " + detalle);
        }
    }
}
#endif
