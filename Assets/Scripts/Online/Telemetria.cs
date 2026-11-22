// ============ LAB13: TELEMETRÍA (Unity Analytics 6.0.3) ============
// El SDK arranca APAGADO: solo registra después de IniciarRecoleccion()
// (lo llama la pantalla de consentimiento cuando el jugador acepta).
// Registrar() NUNCA tumba el juego: todo va en try/catch y con log [DVSC].

using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public static class Telemetria
{
    public static bool Recolectando { get; private set; }
    static bool primerCombateRegistrado;
    static bool tutorialRegistrado;

    // Nombres de eventos: SUSTANTIVO + PARTICIPIO, en snake_case (creados en el Event Manager).
    public static class Eventos
    {
        public const string JuegoIniciado = "juego_iniciado";
        public const string TutorialCompletado = "tutorial_completado";
        public const string PrimerCombateIniciado = "primer_combate_iniciado";
        public const string CombateIniciado = "combate_iniciado";
        public const string CombateGanado = "combate_ganado";
        public const string CombatePerdido = "combate_perdido";
        public const string TiendaAbierta = "tienda_abierta";
        public const string PersonajeComprado = "personaje_comprado";
        public const string CompraFallidaSinFondos = "compra_fallida_sin_fondos";
    }

    // Paso 1: encender el SDK cuando el jugador aceptó (el ExternalUserId marca el perfil).
    // Se asegura primero de que Unity Services esté inicializado y con sesión (anónima).
    public static async System.Threading.Tasks.Task IniciarRecoleccion(string externalUserId = null)
    {
        if (Recolectando) return;
        try
        {
            if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
                await UnityServices.InitializeAsync(); // sirve aunque entres directo a la escena de juego

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync(); // perfil invitado

            if (!string.IsNullOrEmpty(externalUserId))
                UnityServices.ExternalUserId = externalUserId; // 1 x perfil
            AnalyticsService.Instance.StartDataCollection();   // aceptado
            Recolectando = true;
            Debug.Log("[DVSC] Telemetría: recolección INICIADA (ExternalUserId=" +
                      (string.IsNullOrEmpty(externalUserId) ? "(predeterminado)" : externalUserId) + ").");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DVSC] Telemetría no se pudo iniciar: " + e.Message);
        }
    }

    // Pasos 3 a 5: un evento con parámetros TIPADOS (los tipos se definen en el Event Manager).
    public static void Registrar(string nombre, Dictionary<string, object> p = null)
    {
        try
        {
            if (!Recolectando)
            {
                Debug.Log("[DVSC] telemetría OFF (sin consentimiento): " + nombre + " ignorado.");
                return;
            }
            var evento = new CustomEvent(nombre);
            if (p != null) foreach (var par in p) evento[par.Key] = par.Value;
            AnalyticsService.Instance.RecordEvent(evento);

            string detalle = "";
            if (p != null)
                foreach (var par in p)
                    detalle += (detalle == "" ? "" : ", ") + par.Key + "=" + par.Value;
            Debug.Log("[DVSC] evento registrado: " + nombre + (detalle == "" ? "" : " (" + detalle + ")"));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DVSC] la telemetría no tumba el juego: " + e.Message);
        }
    }

    // ---- Puntos 4: combates (heroe, nivel_rival, turnos) ----
    public static void RegistrarCombateIniciado(string heroe, int nivelRival)
    {
        var p = new Dictionary<string, object> { { "heroe", heroe }, { "nivel_rival", nivelRival } };
        Registrar(Eventos.CombateIniciado, p);
        if (!primerCombateRegistrado)
        {
            primerCombateRegistrado = true;
            Registrar(Eventos.PrimerCombateIniciado, p);
        }
    }

    public static void RegistrarResultadoCombate(string heroe, int nivelRival, int turnos, bool gano)
    {
        Registrar(gano ? Eventos.CombateGanado : Eventos.CombatePerdido,
            new Dictionary<string, object>
            {
                { "heroe", heroe }, { "nivel_rival", nivelRival }, { "turnos", turnos }
            });
    }

    // ---- Punto 3: tutorial (segundos) — el "arranque listo" de este juego = primer escuadrón puesto ----
    public static void RegistrarTutorialCompletado(double segundos)
    {
        if (tutorialRegistrado) return;
        tutorialRegistrado = true;
        Registrar(Eventos.TutorialCompletado,
            new Dictionary<string, object> { { "segundos", System.Math.Round(segundos) } });
    }

    // ---- Punto 5: economía (heroe/origen, faltante/precio) — nunca datos personales ----
    public static void RegistrarTiendaAbierta(string origen)
    {
        Registrar(Eventos.TiendaAbierta, new Dictionary<string, object> { { "origen", origen } });
    }

    public static void RegistrarCompra(string heroe, long precio)
    {
        Registrar(Eventos.PersonajeComprado,
            new Dictionary<string, object> { { "heroe", heroe }, { "precio", precio } });
    }

    public static void RegistrarCompraFallida(string heroe, long faltante)
    {
        Registrar(Eventos.CompraFallidaSinFondos,
            new Dictionary<string, object> { { "heroe", heroe }, { "faltante", faltante } });
    }
}
