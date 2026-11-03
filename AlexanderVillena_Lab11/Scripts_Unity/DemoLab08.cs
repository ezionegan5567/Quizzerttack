// ============ LAB08: DEMO DEL COMBATE VERIFICADO (requiere UGS_ON) ============
// Corre en una pasada todo lo que pide la guía, para la captura de consola:
//   1) sesión anónima + el servidor siembra el inventario protegido
//   2) ResolverCombate real (semilla del servidor, battle log, premio)
//   3) RepetirCombate: mismo log, sin volver a premiar            (paso 7)
//   4) Los CINCO intentos de trampa del paso 9 y la respuesta literal del servidor
//
// Montaje: GameObject "Registros" → Add Component → DemoLab08 (y ReproductorCombate).
// Corre con F12 o con el botón que apunte a "Correr Lab08".

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using UnityEngine;

public class DemoLab08 : MonoBehaviour
{
    const string Modulo = "Tienda";

    [Header("Mis héroes (ids de la tabla_heroes)")]
    public List<string> equipo = new List<string> { "guerrero_del_hacha", "vampiro", "draconiano" };

    [Header("Rival NPC (id de rivales_npc)")]
    public string rivalNpc = "npc_practica_1";

    [Header("Reproductor (si se deja vacío se busca en este GameObject)")]
    [SerializeField] ReproductorCombate reproductor;

    bool corriendo;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f12Key.wasPressedThisFrame) Correr();
    }
#else
    void Update() { if (Input.GetKeyDown(KeyCode.F12)) Correr(); }
#endif

    [ContextMenu("Correr Lab08")]
    public async void Correr()
    {
        if (corriendo)
        {
            Debug.Log("[Lab08] La demo YA está corriendo (pulsación ignorada).");
            return;
        }
        corriendo = true;
        string paso = "inicio";

        try
        {
            if (reproductor == null) reproductor = GetComponent<ReproductorCombate>();
            if (reproductor == null)
            {
                Debug.LogError("[Lab08] Falta el componente ReproductorCombate en este GameObject.");
                return;
            }

            Debug.Log("========== LAB08 · COMBATE VERIFICADO EN EL SERVIDOR ==========");

            paso = "1 sesión + inventario protegido";
            await UgsDefensa.IniciarAsync();
            await ProgresionCliente.PublicarInventarioServidorAsync(equipo);

            paso = "2 ResolverCombate";
            var pelea = await reproductor.PelearAsync(equipo, rivalNpc);
            if (pelea == null) { Debug.LogError("[Lab08] No hubo respuesta del combate."); return; }
            if (pelea.Rechazado) return;

            paso = "3 RepetirCombate (mismo log, sin premio)";
            await reproductor.RepetirAsync();

            paso = "4 los cinco intentos de trampa";
            await IntentosTramposos();

            Debug.Log("[Lab08] Demo completo. Captura la consola para el informe.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab08] Falló en «{paso}»: {e.Message}");
        }
        finally
        {
            corriendo = false;
        }
    }

    // ---- Paso 9: lo que el jugador intenta mandar y el servidor ignora ----
    async Task IntentosTramposos()
    {
        // (1) Daño, victoria y semilla inventados por el cliente: no existen como
        //     parámetros de la función, así que se descartan y la pelea la decide el servidor.
        var args1 = new Dictionary<string, object>
        {
            { "equipo", equipo },
            { "idRival", rivalNpc },
            { "dano", 9999 },
            { "victoria", true },
            { "semilla", 123 }
        };
        await Intento("1 dano=9999+victoria=true+semilla=123", args1);

        // (2) El héroe coloso no es peleable (no está en ninguna tabla).
        await Intento("2 heroe_coloso", Args(new List<string> { "heroe_coloso" }, rivalNpc));

        // (3) El mismo héroe dos veces teniendo uno.
        string primero = equipo.Count > 0 ? equipo[0] : "guerrero_del_hacha";
        await Intento("3 héroe repetido", Args(new List<string> { primero, primero }, rivalNpc));

        // (4) Cuatro héroes: el tope es tres.
        var cuatro = new List<string>(equipo) { "bufeador" };
        await Intento("4 cuatro héroes", Args(cuatro, rivalNpc));

        // (5) Un rival que no existe en rivales_npc.
        await Intento("5 rival inexistente", Args(equipo, "npc_inexistente"));

        // (6) Un héroe que no está en mi inventario protegido.
        await Intento("6 héroe que no tengo", Args(new List<string> { "asesino" }, rivalNpc));
    }

    static Dictionary<string, object> Args(List<string> equipo, string idRival)
    {
        return new Dictionary<string, object> { { "equipo", equipo }, { "idRival", idRival } };
    }

    async Task<ResultadoCombate> Intento(string etiqueta, Dictionary<string, object> args)
    {
        try
        {
            var res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(
                Modulo, "ResolverCombate", args);
            Debug.Log($"[Lab08][INTENTO {etiqueta}] servidor → Rechazado={res.Rechazado} " +
                      $"Victoria={res.Victoria} Semilla={res.Semilla} " +
                      $"Saldo={ReproductorCombate.Saldo(res)} " +
                      $"| \"{res.Mensaje}\"");
            return res;
        }
        catch (Exception e)
        {
            Debug.Log($"[Lab08][INTENTO {etiqueta}] servidor → excepción: {e.Message}");
            return null;
        }
    }
}
#endif
