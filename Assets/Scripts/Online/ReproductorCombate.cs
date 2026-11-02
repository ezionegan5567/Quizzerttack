// ============ LAB08: REPRODUCTOR DEL COMBATE (requiere UGS_ON) ============
// Paso 8: el cliente NO simula nada. Llama a Tienda.ResolverCombate y reproduce el
// battle log que devuelve el servidor, una acción cada 0,6 s, pintando la vida con
// el `vidaRestante` tal como viene (nunca se recalcula el daño ni el resultado).
//
// Montaje: GameObject "Registros" → Add Component → ReproductorCombate
// (y, si quieres barras, arrastra dos Sliders a los campos de arriba).

#if UGS_ON
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using UnityEngine;
using UnityEngine.UI;

public class ReproductorCombate : MonoBehaviour
{
    // OJO: es una función del MÓDULO C# "Tienda": CallModuleEndpointAsync.
    const string Modulo = "Tienda";
    const float SegundosPorAccion = 0.6f;

    [Header("Barras de vida (opcionales)")]
    [SerializeField] Slider barraVidaPropio;
    [SerializeField] Slider barraVidaRival;

    // Se disparan por cada línea del log y al terminar la pelea (para enganchar UI).
    public event Action<EntradaBatallaLog> AccionReproducida;
    public event Action<ResultadoCombate> CombateTerminado;

    readonly Dictionary<string, int> _vida = new Dictionary<string, int>();
    readonly List<string> _idsPropios = new List<string>();
    int _vidaInicialPropio;
    int _vidaInicialRival;
    List<EntradaBatallaLog> _ultimoLog;

    // ---- Paso 8: pelear. El equipo y el id del rival son lo ÚNICO que viaja. ----
    public async Task<ResultadoCombate> PelearAsync(List<string> equipo, string idRival)
    {
        // Los argumentos se mapean POR NOMBRE de parámetro: (equipo, idRival) van sueltos.
        var args = new Dictionary<string, object>
        {
            { "equipo", equipo },
            { "idRival", idRival }
        };

        ResultadoCombate res;
        try
        {
            res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(
                Modulo, "ResolverCombate", args);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab08][Combate] Error llamando a ResolverCombate: {e.Message}");
            return null;
        }

        if (res == null) return null;
        if (res.Rechazado)
        {
            Debug.Log($"[Lab08][RECHAZO] El servidor dijo NO: \"{res.Mensaje}\" (no se escribió nada)");
            return res;
        }

        Debug.Log($"[Lab08][Combate] Victoria={res.Victoria} Turnos={res.TurnosJugados} Semilla={res.Semilla} " +
                  $"vs {res.RivalNombre} ({res.Rival}) acciones={res.BattleLog.Count} " +
                  $"premio={res.Recompensa.Moneda}+{res.Recompensa.Cantidad} saldo={res.SaldoResultante}");
        _ultimoLog = res.BattleLog;
        await ReproducirAsync(res);
        return res;
    }

    // ---- Paso 7 (lado cliente): el MISMO combate, sin premio, con log idéntico ----
    public async Task<ResultadoCombate> RepetirAsync()
    {
        ResultadoCombate res;
        try
        {
            res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombate>(
                Modulo, "RepetirCombate", new Dictionary<string, object>());
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab08][Repetición] Error llamando a RepetirCombate: {e.Message}");
            return null;
        }

        if (res == null) return null;
        if (res.Rechazado)
        {
            Debug.Log($"[Lab08][Repetición] \"{res.Mensaje}\"");
            return res;
        }

        bool identico = MismoLog(_ultimoLog, res.BattleLog);
        Debug.Log($"[Lab08][DETERMINISMO] repetición con semilla {res.Semilla}: log idéntico = {identico} " +
                  $"({res.BattleLog.Count} acciones) — saldo sigue en {res.SaldoResultante}");
        _ultimoLog = res.BattleLog;
        await ReproducirAsync(res);
        return res;
    }

    // ---- Reproducción: una acción cada 0,6 s, con la vida que manda el servidor ----
    Task<ResultadoCombate> ReproducirAsync(ResultadoCombate res)
    {
        var tcs = new TaskCompletionSource<ResultadoCombate>();
        StartCoroutine(Reproducir(res, tcs));
        return tcs.Task;
    }

    IEnumerator Reproducir(ResultadoCombate res, TaskCompletionSource<ResultadoCombate> tcs)
    {
        PrepararBarras(res);
        foreach (var accion in res.BattleLog)
        {
            yield return new WaitForSeconds(SegundosPorAccion);

            // La vida sale del log: no se resta daño ni se recalcula el resultado.
            if (accion.Objetivo != null) _vida[accion.Objetivo] = accion.VidaRestante;

            PintarBarras();
            Debug.Log($"[Lab08][Acción] {accion}");
            AccionReproducida?.Invoke(accion);
        }

        Debug.Log($"[Lab08][Fin] {(res.Victoria ? "VICTORIA" : "DERROTA")} tras {res.TurnosJugados} turnos. " +
                  $"Saldo resultante: {res.SaldoResultante}");
        CombateTerminado?.Invoke(res);
        tcs.TrySetResult(res);
    }

    // Las barras arrancan en la vida inicial de EquipoPropio / EquipoRival.
    void PrepararBarras(ResultadoCombate res)
    {
        _vida.Clear();
        _idsPropios.Clear();
        _vidaInicialPropio = 0;
        _vidaInicialRival = 0;

        foreach (var u in res.EquipoPropio)
        {
            _vida[u.Id] = u.Vida;
            _idsPropios.Add(u.Id);
            _vidaInicialPropio += u.Vida;
        }
        foreach (var u in res.EquipoRival) _vida[u.Id] = u.Vida;
        foreach (var u in res.EquipoRival) _vidaInicialRival += u.Vida;

        PintarBarras();
    }

    void PintarBarras()
    {
        if (barraVidaPropio != null && _vidaInicialPropio > 0)
            barraVidaPropio.value = (float)VidaPropia() / _vidaInicialPropio;
        if (barraVidaRival != null && _vidaInicialRival > 0)
            barraVidaRival.value = (float)VidaRival() / _vidaInicialRival;
    }

    int VidaPropia()
    {
        int suma = 0;
        foreach (var id in _idsPropios)
            if (_vida.TryGetValue(id, out var v)) suma += Mathf.Max(0, v);
        return suma;
    }

    int VidaRival()
    {
        int suma = 0;
        foreach (var par in _vida)
            if (!_idsPropios.Contains(par.Key)) suma += Mathf.Max(0, par.Value);
        return suma;
    }

    static bool MismoLog(List<EntradaBatallaLog> a, List<EntradaBatallaLog> b)
    {
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Turno != b[i].Turno) return false;
            if (a[i].Atacante != b[i].Atacante) return false;
            if (a[i].Objetivo != b[i].Objetivo) return false;
            if (a[i].Dano != b[i].Dano) return false;
            if (a[i].VidaRestante != b[i].VidaRestante) return false;
        }
        return true;
    }
}
#endif
