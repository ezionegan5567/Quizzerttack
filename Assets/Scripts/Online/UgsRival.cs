// ============ LAB09: CLIENTE CLOUD CODE — SCRIPTS "T9*" (requiere UGS_ON) ============
// Llama a los Cloud Code scripts desplegados (T9Buscar, T9Combate, T9Inventario).

#if UGS_ON
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using UnityEngine;

public static class UgsRival
{
    const string ScriptBuscar = "T9Buscar";
    const string ScriptCombate = "T9Combate";
    const string ScriptInventario = "T9Inventario";

    // SONDA: invoca script JS "Sonda" — prueba de que Cloud Code responde al cliente
    public static async Task SondaAsync()
    {
        var req = new Dictionary<string, object> { { "nombre", "cli" } };
        var res = await CloudCodeService.Instance.CallEndpointAsync<string>("Sonda", req);
        Debug.Log("[SONDA] Cloud Code respondió: " + res);
    }

    // Pasos 3-5: buscar rival por franja de nivel (el índice público nivel_1 vive en el servidor)
    public static async Task<BuscarRivalResultado> BuscarRivalAsync(int nivel, int franjaMaxima = 10, int suficientes = 2)
    {
        var req = new Dictionary<string, object>
        {
            { "Nivel", nivel },
            { "FranjaMaxima", franjaMaxima },
            { "CandidatosSuficientes", suficientes }
        };
        var res = await CloudCodeService.Instance.CallEndpointAsync<BuscarRivalResultado>(ScriptBuscar, req);
        Debug.Log($"[BuscarRival] Hubo={res.Hubo} franja={res.Franja} consultas={res.Consultas} lecturas={res.LecturasConsumidas} {(res.Hubo ? "rival=" + res.Rival?.Nombre : res.Mensaje)}");
        return res;
    }

    // Paso 6: resolver el combate contra el rival elegido (real o npc)
    public static async Task<ResultadoCombate> ResolverCombateAsync(string rivalId, List<string> equipoIds)
    {
        var req = new Dictionary<string, object>
        {
            { "Rival", rivalId },
            { "Equipo", equipoIds }
        };
        var res = await CloudCodeService.Instance.CallEndpointAsync<ResultadoCombate>(ScriptCombate, req);
        Debug.Log($"[ResolverCombate] Gano={res.Gano} contra {res.RivalNombre} ({res.Fuente}) oro={res.Oro} trofeos={res.Trofeos} — {res.Mensaje}");
        return res;
    }
    // Inventario PROTEGIDO: solo el servidor lo escribe (script, llave "inventario")
    public static async Task PublicarInventarioAsync(List<string> ids)
    {
        var req = new Dictionary<string, object> { { "Equipo", ids } };
        var res = await CloudCodeService.Instance.CallEndpointAsync<string>(ScriptInventario, req);
        Debug.Log($"[Inventario] Protegido en el servidor ({ids.Count} héroes). {res}");
    }
}
#endif
