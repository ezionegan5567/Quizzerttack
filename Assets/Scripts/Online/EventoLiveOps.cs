// ============ LAB12: LIVE OPS EN EL CLIENTE (pasos 6 y 7) ============
// Qué hace el cliente aquí y qué NO hace:
//   SÍ: preguntar VistaEvento cada 15 s, avisar cuando el evento se enciende o se apaga
//       y pintar el estado con una cuenta atrás anclada a la hora del SERVIDOR.
//   NO: aplicar el daño, ni el descuento, ni el botín, ni decidir si es jugador nuevo.
//       Todo eso lo resuelve el módulo (Tienda) con los datos protegidos.
//
// Montaje: desde cualquier script -> EventoLiveOps.Iniciar(); (crea su propio GameObject).
// Requiere: UGS_ON y una sesión iniciada (UgsDefensa.IniciarAsync).

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lab09;
using Unity.Services.CloudCode;
using UnityEngine;

public class EventoLiveOps : MonoBehaviour
{
    public const string Modulo = "Tienda";

    // Paso 6: el intervalo de consulta (15 s). El evento aparece y desaparece SOLO:
    // no hay que reiniciar la aplicación ni publicar una versión nueva.
    public static float SegundosEntreConsulta = 15f;

    public static EventoLiveOps Instancia { get; private set; }

    // Última vista que devolvió el SERVIDOR (null = todavía no preguntó).
    public static VistaEventoDto Campana { get; private set; }

    // Paso 6: se dispara SOLO cuando el evento se enciende o se apaga (no en cada consulta).
    public static event Action<VistaEventoDto> EventoCambio;

    public static bool HayEvento { get { return Campana != null && Campana.Activo; } }

    static bool _arrancado;
    static bool _consultando;
    static float _proximaConsulta;
    static float _instanteDeLectura;
    static long _segundosAlLeer;

    // Arranca la vigilancia (idempotente: se puede llamar desde varios scripts).
    public static void Iniciar(float segundos = 15f)
    {
        SegundosEntreConsulta = Mathf.Max(2f, segundos);
        if (Instancia == null)
        {
            var go = new GameObject("EventoLiveOps (Lab12)");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<EventoLiveOps>();
        }
        if (_arrancado) return;
        _arrancado = true;
        _proximaConsulta = 0f;
        Debug.Log($"[Lab12][EVENTO] vigilando Remote Config cada {SegundosEntreConsulta:0} s " +
                  "(sin reiniciar la aplicación ni publicar versión).");
    }

    public static void Detener() { _arrancado = false; }

    void Update()
    {
        if (!_arrancado || _consultando) return;
        if (Time.unscaledTime < _proximaConsulta) return;
        _proximaConsulta = Time.unscaledTime + SegundosEntreConsulta;
        _ = ActualizarAsync();   // el sondeo no bloquea el hilo de Unity
    }

    // Paso 6: una consulta a VistaEvento. La hora, los segundos y los números son del servidor.
    public static async Task<VistaEventoDto> ActualizarAsync()
    {
        if (_consultando) return Campana;
        _consultando = true;
        try
        {
            var vista = await CloudCodeService.Instance.CallModuleEndpointAsync<VistaEventoDto>(
                Modulo, "VistaEvento", new Dictionary<string, object>());
            if (vista == null)
            {
                Debug.LogWarning("[Lab12][EVENTO] el módulo no devolvió la vista.");
                return Campana;
            }

            _instanteDeLectura = Time.realtimeSinceStartup;
            _segundosAlLeer = vista.SegundosRestantes;

            bool habia = HayEvento;
            Campana = vista;

            Debug.Log($"[Lab12][EVENTO] {vista.Mensaje} | servidor {vista.AhoraUtc} | " +
                      $"quedan {CuentaAtras(SegundosRestantesAhora())} | esNuevo={vista.EsNuevo} | " +
                      $"bono {vista.BonoBienvenida} {vista.MonedaBono} (reclamado={vista.BonoReclamado})");

            if (habia != vista.Activo)
            {
                string texto = vista.Activo
                    ? $">> CAMBIÓ: el evento APARECIÓ «{vista.Nombre}» (quedan {CuentaAtras(SegundosRestantesAhora())})"
                    : ">> CAMBIÓ: el evento DESAPARECIÓ (se vuelve a los números base del S10)";
                Debug.Log($"[Lab12][{DateTime.Now:HH:mm:ss}] {texto}");
                EventoCambio?.Invoke(vista);
            }
            return vista;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Lab12][EVENTO] no se pudo consultar VistaEvento: {e.Message}");
            return Campana;
        }
        finally { _consultando = false; }
    }

    // Paso 7: el contador NO usa el reloj del dispositivo (el jugador podría atrasarlo).
    // Se ancla a los segundos que mandó el servidor y se descuenta con el reloj monótono
    // de Unity: al reconectar o volver del segundo plano se vuelve a preguntar.
    public static long SegundosRestantesAhora()
    {
        if (!HayEvento) return 0;
        var transcurrido = (long)(Time.realtimeSinceStartup - _instanteDeLectura);
        return Math.Max(0, _segundosAlLeer - transcurrido);
    }

    public static string CuentaAtras(long segundos)
    {
        var s = Math.Max(0, segundos);
        return $"{s / 3600:00}:{(s % 3600) / 60:00}:{s % 60:00}";
    }

    // Paso 3 (solo PINTAR): el precio que el servidor cobrará. Si el cliente miente,
    // el cobro sigue siendo el del servidor (ComprarHeroe ignora este cálculo).
    public static long PrecioMostrado(string id, long precioBase)
    {
        if (!HayEvento || Campana.Descuento <= 0 || Campana.HeroesEspeciales == null) return precioBase;
        if (!Campana.HeroesEspeciales.Contains(id)) return precioBase;
        var fraccion = Math.Min(1.0, Campana.Descuento);
        return (long)Math.Round(precioBase * (1.0 - fraccion));
    }

    // Una línea para la consola o un HUD.
    public static string Describir()
    {
        if (Campana == null) return "Evento: sin consultar";
        if (!Campana.Activo) return "Evento: apagado (números base)";
        return $"{Campana.Nombre} · quedan {CuentaAtras(SegundosRestantesAhora())} · " +
               $"daño x{Campana.FactorDano:0.##} · tienda -{Campana.Descuento * 100:0.#} %";
    }
}
#endif
