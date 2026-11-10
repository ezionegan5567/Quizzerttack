// ============ LAB11: demo de la tabla de trofeos (F11) ============
// Corre los pasos 5 a 9 en una sola pasada:
//   1. Cuatro jugadores (perfiles Bruno, Carla, Ana y Dana) pelean uno tras otro: cada
//      combate lo resuelve el servidor y el SERVIDOR publica el total en la tabla.
//   2. Vuelve al perfil principal y consulta: top 10, mi puesto y mis vecinos.
//   3. Intenta enviar 9999 trofeos desde el CLIENTE (trampa): con la política .ac
//      desplegada el servicio la rechaza.
//   4. Pinta la vista de temporada con el reloj del SERVIDOR.
//
// Montaje: en el GameObject "Registros" (el mismo de los otros labs) → F6.
// (F7-F12 ya las usan los demos de los labs anteriores: F7 panel online, F8 rival,
//  F9 registros, F10 Lab 09, F11 Lab 10, F12 Lab 08.)

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Leaderboards;
using UnityEngine;

public class DemoLab11 : MonoBehaviour
{
    [Header("Perfiles que pelean (uno por jugador real)")]
    public string[] perfiles = { "bruno", "carla", "ana", "dana" };

    [Header("Equipo con el que pelea cada perfil")]
    public List<string> equipo = new List<string> { "guerrero_del_hacha", "vampiro", "draconiano" };

    [Header("Rival contra el que pelean (id de rivales_npc)")]
    public string rivalNpc = "npc_practica_2";

    [Header("Perfil con el que se consulta la tabla al final")]
    public string perfilPrincipal = "principal";

    bool _corriendo;

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame)
            Correr();
#else
        if (Input.GetKeyDown(KeyCode.F6)) Correr();
#endif
    }

    [ContextMenu("Correr Lab11")]
    public async void Correr()
    {
        if (_corriendo) { Debug.Log("[Lab11] La demo ya está corriendo."); return; }
        _corriendo = true;
        try
        {
            Debug.Log("========== LAB11 - TABLA DE TROFEOS (LEADERBOARDS) ==========");
            await UgsDefensa.IniciarAsync(perfilPrincipal);
            Debug.Log($"[Lab11][TABLA] id de la tabla: {RankingCliente.Tabla} (el nombre del archivo .lb)");

            // ---- 1) Cuatro jugadores pelean: el SERVIDOR publica el total ----
            foreach (var perfil in perfiles)
            {
                await UgsDefensa.CambiarPerfilAsync(perfil);

                // Nombre UNA sola vez (cada llamada generaría otro sufijo).
                await RankingCliente.PonerNombreUnaVezAsync(PrimeraMayuscula(perfil));

                // El inventario protegido lo escribe el servidor: sin esto no puede pelear.
                await ProgresionCliente.PublicarInventarioServidorAsync(equipo);

                var res = await PelearAsync(equipo, rivalNpc);
                if (res == null) { Debug.LogError($"[Lab11][PERFIL {perfil}] la pelea no devolvió nada"); continue; }

                string situacion = res.Rechazado ? $"RECHAZADO: {res.Mensaje}" :
                    $"{(res.Victoria ? "VICTORIA" : "DERROTA")} · trofeos {(res.TrofeosCambio >= 0 ? "+" : "")}{res.TrofeosCambio} " +
                    $"· puesto en la tabla {res.PuestoEnRanking}";

                Debug.Log($"[Lab11][PERFIL {perfil}] {situacion}");
            }

            // ---- 2) De vuelta al jugador principal: tres vistas de la misma tabla ----
            await UgsDefensa.CambiarPerfilAsync(perfilPrincipal);
            await RankingCliente.PonerNombreUnaVezAsync("Alexander");

            var top = await RankingCliente.RefrescarTopAsync(10);
            var mio = await RankingCliente.MiPuestoAsync();
            var vecinos = await RankingCliente.VecinosAsync(5);

            Debug.Log($"[Lab11][RESUMEN] tabla con {top.Count} filas visibles · " +
                      $"yo: {(mio == null ? "sin clasificar" : mio.ToString())} · " +
                      $"vecinos: {vecinos.Count}");

            // ---- 3) Trampa: el cliente intenta escribir su propio puntaje ----
            await IntentarTrampaAsync();

            // ---- 4) La temporada, con la hora del servidor ----
            await RankingCliente.VistaTemporadaAsync();

            Debug.Log("[Lab11] Demo completa. Captura la consola y el dashboard (Leaderboards → trofeos_dvsc) para el informe.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab11] Error en la demo: {e.Message}");
        }
        finally
        {
            _corriendo = false;
        }
    }

    // La pelea pasa por el módulo: sin animación (aquí lo que importa es la tabla).
    async Task<ResultadoCombate> PelearAsync(List<string> equipoPropio, string idRival)
    {
        try
        {
            var args = new Dictionary<string, object>
            {
                { "equipo", equipoPropio },
                { "idRival", idRival }
            };
            return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(
                "Tienda", "ResolverCombate", args);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab11] Error llamando a ResolverCombate: {e.Message}");
            return null;
        }
    }

    // PASO 8: el cliente manda un puntaje a mano. Sin la política lo ACEPTA; con la
    // política S11-politica-ranking.ac desplegada el servicio responde «Access ... restricted».
    async Task IntentarTrampaAsync()
    {
        try
        {
            var resp = await LeaderboardsService.Instance.AddPlayerScoreAsync(RankingCliente.Tabla, 9999);
            Debug.Log($"[Lab11][TRAMPA] el servicio ACEPTÓ el puntaje 9999 enviado por el CLIENTE " +
                      $"(puesto {resp.Rank + 1}). Sin política .ac desplegada esto es posible: hay que cerrarla.");
        }
        catch (Exception e)
        {
            Debug.Log($"[Lab11][TRAMPA] el servicio RECHAZÓ el puntaje del cliente: {e.GetType().Name} · {e.Message}");
        }
    }

    static string PrimeraMayuscula(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        return char.ToUpperInvariant(texto[0]) + texto.Substring(1);
    }
}
#endif
