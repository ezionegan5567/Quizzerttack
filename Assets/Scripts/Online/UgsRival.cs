// ============ LAB09: CLIENTE CLOUD CODE — MÓDULO "Tienda" (requiere UGS_ON) ============
// Llama a las funciones del módulo desplegado (BuscarRival, ResolverCombate).

#if UGS_ON
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using UnityEngine;

public static class UgsRival
{
    const string Modulo = "Tienda";

    // Pasos 3-5: buscar rival por franja de nivel (el índice público nivel_1 vive en el servidor)
    public static async Task<BuscarRivalResultado> BuscarRivalAsync(int nivel, int franjaMaxima = 10, int suficientes = 2)
    {
        var req = new Dictionary<string, object>
        {
            { "PlayerId", UgsDefensa.MiPlayerId },
            { "Nivel", nivel },
            { "FranjaMaxima", franjaMaxima },
            { "CandidatosSuficientes", suficientes }
        };
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<BuscarRivalResultado>(Modulo, "BuscarRival", req);
        Debug.Log($"[BuscarRival] Hubo={res.Hubo} franja={res.Franja} consultas={res.Consultas} lecturas={res.LecturasConsumidas} {(res.Hubo ? "rival=" + res.Rival?.Nombre : res.Mensaje)}");
        return res;
    }

    // Paso 6: resolver el combate contra el rival elegido (real o npc)
    public static async Task<ResultadoCombate> ResolverCombateAsync(string rivalId, List<string> equipoIds)
    {
        var req = new Dictionary<string, object>
        {
            { "PlayerId", UgsDefensa.MiPlayerId },
            { "Rival", rivalId },
            { "Equipo", equipoIds }
        };
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(Modulo, "ResolverCombate", req);
        Debug.Log($"[ResolverCombate] Gano={res.Gano} contra {res.RivalNombre} ({res.Fuente}) oro={res.Oro} trofeos={res.Trofeos} — {res.Mensaje}");
        return res;
    }
    // Inventario PROTEGIDO: solo el servidor lo escribe (función del módulo, llave "inventario")
    public static async Task PublicarInventarioAsync(List<string> ids)
    {
        var req = new Dictionary<string, object> { { "Equipo", ids } };
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(Modulo, "PublicarInventario", req);
        Debug.Log($"[Inventario] Protegido en el servidor ({ids.Count} héroes). {res}");
    }
}
#endif
