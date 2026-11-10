// ============ LAB12: DEMO DEL EVENTO (F5) ============
// Corre los pasos 2, 3, 4, 6, 7 y 8 en una sola pasada:
//   1. Enciende la vigilancia del evento (VistaEvento cada 15 s) y el HUD.
//   2. Muestra el precio del Mago con y sin evento (el servidor cobra, no el cliente).
//   3. Pelea varias veces: el daño va escalado por el evento y RepetirCombate sale igual.
//   4. Compra el Mago: el descuento lo aplica el servidor al cobrar.
//   5. Combate con progresión: el botín incluye el ítem exclusivo si cae.
//   6. Reclama el bonus de bienvenida DOS veces (la segunda debe rechazarse).
//
// Montaje: en el GameObject "Registros" (el mismo de los otros labs) → Play → F5.
// F5 estaba libre (F6 Lab11, F7 online, F8 rival, F9 registros, F10 Lab09,
// F11 Lab10, F12 Lab08). Requiere UGS_ON y el módulo Tienda desplegado.

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using UnityEngine;

public class DemoLab12 : MonoBehaviour
{
    [Header("Perfil con el que corre la demo")]
    public string perfil = "principal";

    [Header("Equipo con el que se pelea (ids del inventario)")]
    public List<string> equipo = new List<string> { "guerrero_del_hacha", "vampiro", "draconiano" };

    [Header("Rival de la tabla (id de rivales_npc)")]
    public string rivalNpc = "npc_jefe_1";

    [Header("Héroe de la oferta y su precio de catálogo")]
    public string heroeEnOferta = "mago";
    public int precioCatalogo = 1200;

    [Header("Combates para juntar oro antes de comprar")]
    public int combates = 6;

    bool _corriendo;

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f5Key.wasPressedThisFrame)
            Correr();
#else
        if (Input.GetKeyDown(KeyCode.F5)) Correr();
#endif
    }

    [ContextMenu("Correr Lab12")]
    public async void Correr()
    {
        if (_corriendo) { Debug.Log("[Lab12] La demo ya está corriendo."); return; }
        _corriendo = true;
        try
        {
            Debug.Log("========== LAB12 - LIVE OPS: EVENTO PROGRAMADO ==========");
            await UgsDefensa.IniciarAsync(perfil);

            // ---- 1) Vigilancia + HUD (pasos 6 y 7) ----
            EventoLiveOps.Iniciar();
            HudEventoLab12.CrearSiFalta();
            var vista = await EventoLiveOps.ActualizarAsync();
            if (vista == null)
            {
                Debug.LogError("[Lab12] VistaEvento no devolvió nada: ¿está desplegado el módulo Tienda?");
                return;
            }
            Debug.Log($"[Lab12][PASO 6] activo={vista.Activo} «{vista.Nombre}» ventana " +
                      $"{vista.InicioUtc} .. {vista.FinUtc} (UTC) · hora del servidor {vista.AhoraUtc} · " +
                      $"quedan {EventoLiveOps.CuentaAtras(EventoLiveOps.SegundosRestantesAhora())}");
            Debug.Log($"[Lab12][PASO 6] servidor dice: {vista.Mensaje}");
            Debug.Log($"[Lab12][PASO 7] HUD: {EventoLiveOps.Describir()}");

            // El inventario protegido lo escribe el servidor: sin esto no puede pelear.
            await ProgresionCliente.PublicarInventarioServidorAsync(equipo);

            // ---- 6) Bonus de bienvenida, dos veces (paso 8) ----
            // Va PRIMERO a propósito: el segmento es «nivel de cuenta <= 1» y cada combate
            // da experiencia (nivel 2 se alcanza con 2 combates). Un jugador nuevo reclama
            // el bono al llegar, antes de pelear: por eso el reclamo va acá y no al final.
            var bono1 = await ReclamarBonoAsync();
            var bono2 = await ReclamarBonoAsync();
            Debug.Log($"[Lab12][PASO 8] esNuevo={vista.EsNuevo} · bono en la vista={vista.BonoBienvenida} {vista.MonedaBono}");
            Debug.Log($"[Lab12][PASO 8] primer reclamo: {bono1?.Mensaje} (saldo {bono1?.Saldo})");
            Debug.Log($"[Lab12][PASO 8] segundo reclamo: {bono2?.Mensaje} (yaReclamado={bono2?.YaReclamado})");

            // ---- 2) El precio, con y sin evento (paso 3) ----
            long mostrado = EventoLiveOps.PrecioMostrado(heroeEnOferta, precioCatalogo);
            Debug.Log($"[Lab12][PASO 3] {heroeEnOferta}: catálogo {precioCatalogo} -> en pantalla {mostrado}" +
                      (vista.Activo ? $" (descuento {vista.Descuento * 100:0.#} %)" : " (sin evento)"));
            var intento = await ComprarAsync(heroeEnOferta);
            Debug.Log($"[Lab12][PASO 3] primer intento de compra: {intento?.Mensaje}");

            // ---- 3) Combates con el factor de daño + repetición idéntica (paso 2) ----
            ResultadoCombate primero = null;
            ResultadoCombate ultimo = null;
            for (var i = 1; i <= Math.Max(1, combates); i++)
            {
                var pelea = await PelearAsync();
                if (pelea == null) break;
                if (primero == null) primero = pelea;
                ultimo = pelea;

                Debug.Log($"[Lab12][PASO 2] combate {i}: {pelea.Mensaje} · evento={pelea.EventoActivo} " +
                          $"«{pelea.EventoNombre}» · factor {pelea.FactorDano:0.##} · " +
                          $"turnos {pelea.TurnosJugados} · oro del combate {pelea.Oro}");
            }

            if (ultimo != null)
            {
                // RepetirCombate repite el ULTIMO combate guardado, no el primero.
                var repetido = await RepetirAsync();
                if (repetido != null)
                {
                    int acciones = ultimo.BattleLog == null ? 0 : ultimo.BattleLog.Count;
                    int accionesRep = repetido.BattleLog == null ? 0 : repetido.BattleLog.Count;
                    bool identico = repetido.TurnosJugados == ultimo.TurnosJugados && acciones == accionesRep;
                    Debug.Log($"[Lab12][PASO 2] RepetirCombate: turnos {repetido.TurnosJugados} vs {ultimo.TurnosJugados} · " +
                              $"acciones {accionesRep} vs {acciones} · MISMO LOG={identico}");
                }
            }

            // ---- 4) La compra, ya con oro (paso 3) ----
            var compra = await ComprarAsync(heroeEnOferta);
            Debug.Log($"[Lab12][PASO 3] compra final: {compra?.Mensaje} · pagó {compra?.Precio} " +
                      $"(catálogo {compra?.PrecioBase}, descuento aplicado={compra?.DescuentoAplicado})");

            // ---- 5) Botín exclusivo del evento (paso 4) ----
            var progresion = await ProgresionCliente.ResolverCombateProgresionAsync(
                rivalNpc, equipo, 0, Guid.NewGuid().ToString("N"));
            if (progresion != null)
                Debug.Log($"[Lab12][PASO 4] combate con progresión: botín=[{string.Join(", ", progresion.Botin)}] · " +
                          $"factor {progresion.FactorDano:0.##} · evento={progresion.EventoActivo}");
            else
                Debug.LogWarning("[Lab12][PASO 4] el combate con progresión no devolvió nada.");

            // ---- 6) Bonus de bienvenida (paso 8): se reclama al principio, ver arriba ----

            Debug.Log("[Lab12] Demo completa. Dejá la consola abierta: el evento se enciende y se apaga " +
                      "solo cada 15 s (grabá el video para el entregable).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab12] Error en la demo: {e.GetType().Name} · {e.Message}");
        }
        finally
        {
            _corriendo = false;
        }
    }

    // El combate del S08: el servidor nace la semilla, aplica el evento y congela el log.
    async Task<ResultadoCombate> PelearAsync()
    {
        try
        {
            var args = new Dictionary<string, object>
            {
                { "equipo", equipo },
                { "idRival", rivalNpc }
            };
            return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(
                "Tienda", "ResolverCombate", args);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab12] Error llamando a ResolverCombate: {e.Message}");
            return null;
        }
    }

    // Repite el ÚLTIMO combate: mismos equipos congelados, misma semilla, mismo log.
    async Task<ResultadoCombate> RepetirAsync()
    {
        try
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(
                "Tienda", "RepetirCombate", new Dictionary<string, object>());
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab12] Error llamando a RepetirCombate: {e.Message}");
            return null;
        }
    }

    // El cliente manda SOLO el id: el precio y el descuento los decide el servidor.
    async Task<ResultadoCompraDto> ComprarAsync(string idHeroe)
    {
        try
        {
            var args = new Dictionary<string, object> { { "idHeroe", idHeroe } };
            return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCompraDto>(
                "Tienda", "ComprarHeroe", args);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab12] Error llamando a ComprarHeroe: {e.Message}");
            return null;
        }
    }

    // Sin argumentos: la cantidad la decide el override del segmento «nuevo».
    async Task<ResultadoBonoDto> ReclamarBonoAsync()
    {
        try
        {
            return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoBonoDto>(
                "Tienda", "ReclamarBonusBienvenida", new Dictionary<string, object>());
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab12] Error llamando a ReclamarBonusBienvenida: {e.Message}");
            return null;
        }
    }
}
#endif
