// ============ LAB11: CLIENTE DE LA TABLA DE TROFEOS (Leaderboards) ============
// Lo que el cliente hace aquí y lo que NO hace:
//   SÍ: pedir el top, su propia posición y sus vecinos (el servicio ordena y pagina),
//       poner su nombre UNA sola vez y preguntar cuánto falta para el cierre.
//   NO: escribir en la tabla (lo hace el servidor con el token del servicio), ni sumar
//       trofeos, ni ordenar, ni calcular el puesto.
// Requiere: com.unity.services.leaderboards (2.3.4) + UGS_ON.

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

public static class RankingCliente
{
    public const string Modulo = "Tienda";
    public const string Tabla = "trofeos_dvsc";   // = nombre del archivo trofeos_dvsc.lb

    // ---------- PASO 5: el top 10 (tres vistas de la misma tabla) ----------
    public static async Task<List<EntradaTablaRanking>> RefrescarTopAsync(int limite = 10)
    {
        try
        {
            var top = await LeaderboardsService.Instance.GetScoresAsync(
                Tabla, new GetScoresOptions { Offset = 0, Limit = limite });

            var filas = Convertir(top);
            Debug.Log($"[Lab11][TOP] {filas.Count} de {top.Total} entradas · ordena el SERVICIO, no el cliente:");
            foreach (var fila in filas) Debug.Log($"[Lab11][TOP]   {fila}");
            return filas;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab11][TOP] No se pudo leer la tabla <{Tabla}>: {e.Message}");
            return new List<EntradaTablaRanking>();
        }
    }

    // ---------- PASO 6: mi posición y mi tier ----------
    // Si el jugador aún no tiene puntaje el servicio responde 404: eso NO es un fallo,
    // la interfaz debe decir «sin clasificar» (null aquí).
    public static async Task<EntradaTablaRanking> MiPuestoAsync()
    {
        try
        {
            var entrada = await LeaderboardsService.Instance.GetPlayerScoreAsync(Tabla);
            var fila = Convertir(entrada);
            Debug.Log($"[Lab11][MÍO] {fila}");
            return fila;
        }
        catch (Exception e)
        {
            Debug.Log($"[Lab11][MÍO] Aún sin clasificar (404 = todavía no puntuaste, no es un error): {e.Message}");
            return null;
        }
    }

    // ---------- PASO 6: los vecinos (el que gana mira arriba, el que pierde también) ----------
    public static async Task<List<EntradaTablaRanking>> VecinosAsync(int rango = 5)
    {
        try
        {
            var vecinos = await LeaderboardsService.Instance.GetPlayerRangeAsync(
                Tabla, new GetPlayerRangeOptions { RangeLimit = rango });

            var filas = Convertir(vecinos);
            Debug.Log($"[Lab11][VECINOS] rango ±{rango} (en los extremos el servicio devuelve lo que quede):");
            foreach (var fila in filas) Debug.Log($"[Lab11][VECINOS]   {fila}");
            return filas;
        }
        catch (Exception e)
        {
            Debug.Log($"[Lab11][VECINOS] Sin vecinos todavía: {e.Message}");
            return new List<EntradaTablaRanking>();
        }
    }

    // ---------- PASO 2: el nombre, UNA sola vez ----------
    // UpdatePlayerNameAsync guarda "Ana" como "Ana#60257" y CADA llamada genera OTRO
    // sufijo: la tabla mostraría jugadores distintos. Por eso solo se pone si no hay nombre.
    public static async Task<string> PonerNombreUnaVezAsync(string nombre)
    {
        var auth = AuthenticationService.Instance;
        if (!string.IsNullOrEmpty(auth.PlayerName))
        {
            Debug.Log($"[Lab11][NOMBRE] ya tiene nombre ({auth.PlayerName}): NO se vuelve a poner");
            return auth.PlayerName;
        }

        await auth.UpdatePlayerNameAsync(nombre);
        Debug.Log($"[Lab11][NOMBRE] nombre puesto UNA vez: {auth.PlayerName} (el sufijo lo añade el servicio)");
        return auth.PlayerName;
    }

    // ---------- PASO 9: la temporada, con la hora del SERVIDOR ----------
    // El cliente no tiene el reloj bueno (se puede adelantar), así que el cierre, el puesto
    // y el premio los calcula el módulo y aquí solo se pintan.
    public static async Task<VistaTemporadaDto> VistaTemporadaAsync()
    {
        try
        {
            var vista = await CloudCodeService.Instance.CallModuleEndpointAsync<VistaTemporadaDto>(
                Modulo, "VistaTemporada", new Dictionary<string, object>());

            if (vista == null)
            {
                Debug.LogError("[Lab11][TEMPORADA] El módulo no devolvió la vista.");
                return null;
            }

            var falta = TimeSpan.FromSeconds(vista.SegundosRestantes);
            Debug.Log($"[Lab11][TEMPORADA] cierra {vista.CierreUtc} (fuente: {vista.FuenteCierre}) · " +
                      $"faltan {(int)falta.TotalDays}d {falta.Hours}h {falta.Minutes}m · " +
                      $"trofeos {vista.Trofeos} · puesto {vista.Puesto} · tier {vista.Tier} · " +
                      $"premio {vista.Premio?.Nombre} (+{vista.Premio?.Gemas} GEMAS)");
            return vista;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lab11][TEMPORADA] Error llamando a VistaTemporada: {e.Message}");
            return null;
        }
    }

    // ---------- Conversión: Rank (0..N) -> Puesto (1..N) ----------
    // OJO con los dos tipos del SDK 2.3.4:
    //   GetScoresAsync        -> LeaderboardScoresPage (trae Results + Total + Offset/Limit)
    //   GetPlayerRangeAsync   -> LeaderboardScores     (solo Results, sin Total)
    static List<EntradaTablaRanking> Convertir(LeaderboardScoresPage pagina)
    {
        return ConvertirLista(pagina?.Results);
    }

    static List<EntradaTablaRanking> Convertir(LeaderboardScores puntajes)
    {
        return ConvertirLista(puntajes?.Results);
    }

    static List<EntradaTablaRanking> ConvertirLista(List<LeaderboardEntry> entradas)
    {
        var filas = new List<EntradaTablaRanking>();
        if (entradas == null) return filas;
        foreach (var entrada in entradas) filas.Add(Convertir(entrada));
        return filas;
    }

    static EntradaTablaRanking Convertir(LeaderboardEntry entrada)
    {
        if (entrada == null) return null;
        return new EntradaTablaRanking
        {
            Puesto = entrada.Rank + 1,                          // el servicio cuenta desde 0
            PlayerId = entrada.PlayerId ?? "",
            Nombre = string.IsNullOrEmpty(entrada.PlayerName) ? "(sin nombre)" : entrada.PlayerName,
            Trofeos = (int)entrada.Score,
            Tier = $"{entrada.Tier}"                            // lo marca el servicio (tieringConfig)
        };
    }
}
#endif
