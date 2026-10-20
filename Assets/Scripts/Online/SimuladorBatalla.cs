using System.Collections.Generic;
using UnityEngine;

// ============ ONLINE: SIMULACIÓN DE BATALLAS ============
// Combate automático (mi escuadrón vs escuadrón guardado en la nube)
// REUSANDO el motor real de Peleador. Los peleadores de simulación:
//  - viven en un GameObject temporal "BatallaSimulada" (spoiler: se borra al final)
//  - no crean panel de habilidades ni barra de vida (esSimulacion)
//  - están aislados de los peleadores reales de la partida (filtro esSimulacion)

public static class SimuladorBatalla
{
    const int maxRondas = 40;

    public static string Simular(RegistroDefensivo enemigo)
    {
        if (enemigo == null || enemigo.peleadores == null || enemigo.peleadores.Count == 0)
            return "Registro enemigo vacío.";
        if (UnityEngine.Object.FindFirstObjectByType<GestorTurnos>() == null)
        {
            Debug.LogWarning("[Simulador] Falta GestorTurnos en la escena.");
        }

        GameObject root = new GameObject("BatallaSimulada");
        string resultado;
        try
        {
            Peleador.LimpiarEstaticos();

            // Mi lado (P1): lectura de las cartas P1 de la escena; si faltan uso F1
            List<string> nombres = new List<string>();
            List<int[]> stats = new List<int[]>();
            GestorTurnos gestor = UnityEngine.Object.FindFirstObjectByType<GestorTurnos>();
            if (gestor != null && gestor.cartasPlayer1 != null)
                foreach (GameObject g in gestor.cartasPlayer1)
                {
                    if (g == null) continue;
                    CartaSeleccion c = g.GetComponent<CartaSeleccion>();
                    if (c == null) continue;
                    nombres.Add(c.nombreHeroe);
                    stats.Add(new int[] { c.vida, c.ataque });
                }
            if (nombres.Count == 0)
            {
                Debug.Log("[Simulador] Sin cartas en escena: uso escuadrón default F1.");
                nombres.AddRange(new string[] { "Bufeador", "Asesino", "Lanza con escudo", "Enano", "Vampiro" });
                stats.AddRange(new int[][] { new int[] { 90, 15 }, new int[] { 80, 32 }, new int[] { 150, 16 }, new int[] { 125, 24 }, new int[] { 110, 28 } });
            }
            for (int i = 0; i < nombres.Count; i++)
            {
                Peleador p = Crear(root, nombres[i], 1, i % 3);
                if (i < stats.Count)
                {
                    p.AjustarVidaInicial(stats[i][0]);
                    p.AjustarAtaqueInicial(stats[i][1]);
                }
            }

            // Enemigo (P2): escuadrón guardado del registro
            for (int i = 0; i < enemigo.peleadores.Count && i < 5; i++)
            {
                PeleadorRegistro pr = enemigo.peleadores[i];
                Peleador p = Crear(root, string.IsNullOrEmpty(pr.nombre) ? "Misterio" : pr.nombre, 2, i % 3);
                if (pr.vida > 0) p.AjustarVidaInicial(pr.vida);
                if (pr.ataque > 0) p.AjustarAtaqueInicial(pr.ataque);
            }

            int rondas = 0;
            int vivos1 = 0, vivos2 = 0;
            while (rondas < maxRondas)
            {
                rondas++;
                List<Peleador> campo = Vivos(root);
                campo.Sort((a, b) =>
                {
                    int d = a.jugadorDueno.CompareTo(b.jugadorDueno);
                    return d != 0 ? d : a.indiceCasilla.CompareTo(b.indiceCasilla);
                });
                foreach (Peleador p in campo)
                {
                    if (p == null || !p.EstaVivo()) continue;
                    p.PrepararSimulacion(p.MejorIndiceSimulacion());
                    p.EjecutarGuardada();
                }
                foreach (Peleador p in campo)
                    if (p != null) p.TickRonda();
                foreach (Peleador p in campo)
                    if (p != null) p.ReiniciarPeleador();

                vivos1 = 0; vivos2 = 0;
                foreach (Peleador p in Vivos(root))
                {
                    if (p.jugadorDueno == 1) vivos1++;
                    else vivos2++;
                }
                if (vivos1 == 0 || vivos2 == 0) break;
            }

            if (vivos1 > 0 && vivos2 == 0)
                resultado = $"¡Tu escuadrón GANA vs {enemigo.jugador}! ({rondas} rondas, vivos {vivos1}/{nombres.Count})";
            else if (vivos2 > 0 && vivos1 == 0)
                resultado = $"Perdiste vs {enemigo.jugador}... ({rondas} rondas, enemigos vivos {vivos2})";
            else
                resultado = $"EMPATE vs {enemigo.jugador} tras {rondas} rondas (vivos {vivos1} vs {vivos2}).";
            Debug.Log("[Simulador] " + resultado);
        }
        finally
        {
            if (root != null) Destroy(root);
            Peleador.LimpiarEstaticos();
        }
        return resultado;
    }

    static void Destroy(GameObject go)
    {
        UnityEngine.Object.Destroy(go);
    }

    static Peleador Crear(GameObject root, string nombre, int jugador, int casilla)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(root.transform, false);
        Peleador p = go.AddComponent<Peleador>();
        p.esSimulacion = true;
        p.nombrePeleador = nombre;
        p.jugadorDueno = jugador;
        p.indiceCasilla = casilla;
        p.IniciarSimulacion();
        return p;
    }

    static List<Peleador> Vivos(GameObject root)
    {
        var r = new List<Peleador>();
        foreach (Peleador p in root.GetComponentsInChildren<Peleador>())
            if (p != null && p.EstaVivo()) r.Add(p);
        return r;
    }
}
