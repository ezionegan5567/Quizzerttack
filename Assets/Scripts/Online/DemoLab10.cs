// ============ LAB10: DEMO DE PROGRESIÓN (requiere UGS_ON) ============
// Corre TODO el flujo del Lab10 en una pasada para tus capturas:
//   1) sesión anónima + sembrar inventario protegido (el servidor lo escribe)
//   2) ResolverCombateProgresion con semilla + idOperación (GUID)  -> Repetida=false (premia)
//   3) MISMA idOperación otra vez                                   -> Repetida=true (no premia, saldo igual)
//   4) DesbloquearNodo (paso 9)                                     -> éxito o motivo del rechazo
//   5) MiProgresoAsync                                              -> imprime las 6 llaves protegidas (paso 7)
// Montaje: GameObject "Registros" → Add Component → DemoLab10. Corre con F11,
// o engancha un Botón (onClick) al método público "Correr Lab10".

#if UGS_ON
using System.Collections.Generic;
using Lab09;
using UnityEngine;

public class DemoLab10 : MonoBehaviour
{
    [Header("Equipo que declaras (ids de la tabla del servidor)")]
    public List<string> equipo = new List<string> { "guerrero_del_hacha", "vampiro", "draconiano" };

    [Header("Rival NPC (no hace falta una 2ª cuenta)")]
    public string rivalNpc = "npc_practica_1";

    [Header("Semilla fija -> combate determinista (repetible en pantalla)")]
    public int semilla = 12345;

    [Header("Nodo a intentar desbloquear (paso 9)")]
    public string nodo = "TALENTO_ATAQUE_1";

    bool corriendo;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f11Key.wasPressedThisFrame) Correr();
    }
#else
    void Update() { if (Input.GetKeyDown(KeyCode.F11)) Correr(); }
#endif

    [ContextMenu("Correr Lab10")]
    public async void Correr()
    {
        if (corriendo)
        {
            Debug.Log("[Lab10] La demo YA está corriendo (ignorada esta pulsación).");
            return;
        }
        corriendo = true;
        string paso = "inicio";
        try
        {
            Debug.Log("========== LAB10 · PROGRESIÓN POST-COMBATE ==========");

            // 1) sesión anónima + inventario protegido sembrado por el servidor
            paso = "1 IniciarAsync";
            await UgsDefensa.IniciarAsync("principal");
            paso = "1 PublicarInventarioServidorAsync";
            await ProgresionCliente.PublicarInventarioServidorAsync(equipo);

            // 2) primer intento: idOperación nueva (GUID) -> debe premiar
            string idOperacion = System.Guid.NewGuid().ToString();
            Debug.Log($"-- idOperación de esta batalla: {idOperacion} --");
            paso = "2 ResolverCombateProgresion (1º)";
            var r1 = await ProgresionCliente.ResolverCombateProgresionAsync(rivalNpc, equipo, semilla, idOperacion);

            // 3) MISMO idOperación otra vez -> idempotencia: Repetida=true, sin volver a premiar
            paso = "3 ResolverCombateProgresion (reintento misma idOperación)";
            var r2 = await ProgresionCliente.ResolverCombateProgresionAsync(rivalNpc, equipo, semilla, idOperacion);
            Debug.Log($"[Lab10][IDEMPOTENCIA] 1º Repetida={r1.Repetida} xp=+{r1.Xp} botin=[{string.Join(",", r1.Botin)}]  |  " +
                      $"2º Repetida={r2.Repetida} xp=+{r2.Xp} botin=[{string.Join(",", r2.Botin)}]  " +
                      $"(el saldo NO volvió a moverse en el 2º)");

            // 4) desbloquear un nodo del árbol (paso 9)
            paso = "4 DesbloquearNodoAsync";
            await ProgresionCliente.DesbloquearNodoAsync(nodo);

            // 5) leo mis 6 llaves protegidas para la captura del paso 7
            paso = "5 MiProgresoAsync";
            await ProgresionCliente.MiProgresoAsync();

            Debug.Log("========== LAB10 listo. Captura esta consola (paso 8) y el Data storage (paso 7). ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Lab10] Falló en el PASO '{paso}':\n{DetalleExcepcion(e)}");
        }
        finally
        {
            corriendo = false;
        }
    }

    // Detalle real de una excepción de Cloud Code (mismo patrón que DemoLab09).
    static string DetalleExcepcion(System.Exception e)
    {
        var sb = new System.Text.StringBuilder();
        var cc = e as Unity.Services.CloudCode.CloudCodeException;
        if (cc != null)
        {
            sb.AppendLine($"Reason={cc.Reason} ErrorCode={cc.ErrorCode}");
            sb.AppendLine(cc.Message);
        }
        else
        {
            sb.AppendLine($"[{e.GetType().Name}] {e.Message}");
        }
        for (var ex = e.InnerException; ex != null; ex = ex.InnerException)
            sb.AppendLine($"  <-- [{ex.GetType().Name}] {e.Message}");
        return sb.ToString();
    }
}
#endif
