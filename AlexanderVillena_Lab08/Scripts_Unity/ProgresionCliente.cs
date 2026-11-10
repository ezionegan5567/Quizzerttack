// ============ LAB10: CLIENTE CLOUD CODE — PROGRESIÓN (requiere UGS_ON) ============
// Llama a las funciones del MÓDULO C# "Tienda" que dan progresión post-combate:
//   - PublicarInventario        (el servidor siembra la llave protegida "inventario")
//   - ResolverCombateProgresion (paso 6: combate + xp/niveles/botín/economía en 1 batch)
//   - DesbloquearNodo           (paso 9: árbol de progresión pagando con moneda)
//   - MiProgresoAsync           (lee las 6 llaves protegidas propias: para la captura del paso 7)
// Reutiliza la sesión anónima de UgsDefensa.IniciarAsync().

#if UGS_ON
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;

public static class ProgresionCliente
{
    // OJO: son funciones de un MÓDULO C#, no de un script JS. Hay que usar
    // CallModuleEndpointAsync(módulo, función, args); CallEndpointAsync busca en SCRIPTS -> 404.
    const string Modulo = "Tienda";

    // El servidor escribe la llave PROTEGIDA "inventario" con stats de tabla (formato {heroes:[...]}).
    public static async Task PublicarInventarioServidorAsync(List<string> ids)
    {
        // El parámetro del módulo se llama "req": hay que anidar el cuerpo bajo esa clave.
        var cuerpo = new Dictionary<string, object> { { "Equipo", ids } };
        var req = new Dictionary<string, object> { { "req", cuerpo } };
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(Modulo, "PublicarInventario", req);
        Debug.Log($"[Lab10][Inventario] Servidor sembró el inventario protegido: {res}");
    }

    // Paso 6: combate con progresión. semilla e idOperación (GUID) viajan al servidor.
    public static async Task<ResultadoCombate> ResolverCombateProgresionAsync(
        string rivalId, List<string> equipoIds, int semilla, string idOperacion)
    {
        var cuerpo = new Dictionary<string, object>
        {
            { "Rival", rivalId },
            { "Equipo", equipoIds },
            { "Semilla", semilla },
            { "IdOperacion", idOperacion }
        };
        var req = new Dictionary<string, object> { { "req", cuerpo } };
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(Modulo, "ResolverCombateProgresion", req);
        Debug.Log(
            $"[Lab10][Combate] Repetida={res.Repetida} Gano={res.Gano} vs {res.RivalNombre} ({res.Fuente}) " +
            $"xp=+{res.Xp} nivel={res.Nivel} subieron=[{string.Join(",", res.Subieron)}] " +
            $"botin=[{string.Join(",", res.Botin)}] oro=+{res.Oro} trofeos=+{res.Trofeos} — {res.Mensaje}");
        return res;
    }

    // Paso 9: desbloquear un nodo del árbol (solo viaja el id; requisitos/precio salen del servidor).
    public static async Task<ResultadoNodo> DesbloquearNodoAsync(string idNodo)
    {
        var req = new Dictionary<string, object> { { "idNodo", idNodo } };
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoNodo>(Modulo, "DesbloquearNodo", req);
        Debug.Log(
            $"[Lab10][Nodo] {idNodo}: Exito={res.Exito} saldo={res.MonedaRestante} " +
            $"desbloqueados=[{string.Join(",", res.Desbloqueos)}] — {res.Mensaje}");
        return res;
    }

    // Paso 7 (captura): leo MIS 6 llaves protegidas (el cliente puede leer sus datos protegidos).
    public static async Task MiProgresoAsync()
    {
        var keys = new HashSet<string>
        {
            Llaves.Inventario, Llaves.Operaciones, Llaves.Perfil,
            Llaves.Progreso, Llaves.Saldos, Llaves.UltimoCombate
        };
        // OJO: el servidor escribió estas llaves como PROTEGIDAS (SetProtectedItemAsync).
        // El LoadAsync por defecto lee la clase "Default" (privada) -> no las vería.
        // Hay que pedir explícitamente la clase Protegida del propio jugador.
        var datos = await CloudSaveService.Instance.Data.Player.LoadAsync(
            keys, new LoadOptions(new ProtectedReadAccessClassOptions()));
        Debug.Log("[Lab10][Progreso] Mis 6 llaves protegidas:");
        foreach (var k in keys)
        {
            if (datos.TryGetValue(k, out var v) && v != null && v.Value != null)
                Debug.Log($"   · {k} = {v.Value.GetAsString()}");
            else
                Debug.Log($"   · {k} = (aún sin datos)");
        }
    }
}
#endif
