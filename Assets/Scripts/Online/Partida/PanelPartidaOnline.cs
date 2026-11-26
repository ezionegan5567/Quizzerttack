// ============ SALA PvP ONLINE POR TURNOS (requiere UGS_ON) ============
// Sala de emparejamiento mostrada al entrar desde el botón Jugar del menú principal:
// crear/unirse, colocar, elegir habilidad, responder el quiz, pasar y abandonar. Dibuja SOLO lo que
// dice la vista del servidor y no decide nada. Se actualiza por push (aviso {type, matchId} -> GetMatchState)
// con un sondeo de respaldo cada ~10 s.
//
// Montaje: en el GameObject "Registros" (o cualquiera) -> Add Component -> PanelPartidaOnline.
// Para probar con UN solo Editor: usa dos perfiles (campo "perfil" + botón "Cambiar perfil"); cada
// perfil es un jugador anónimo distinto. Con dos Editores (o ParrelSync) cada uno usa su perfil por defecto.
//
// En la escena real, EscenaOnline conecta las cartas y los peleadores a la vista autoritativa del servidor.

#if UGS_ON
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuizzerAttack.Partida
{
    [DefaultExecutionOrder(-10000)]
    public class PanelPartidaOnline : MonoBehaviour
    {
        /// <summary>Señal de un solo uso desde Lab01: abre el lobby al entrar a la escena de batalla.</summary>
        public static bool EntradaDesdeMenuPvP;

        [Header("Perfil (para jugar los dos lados desde un solo Editor)")]
        public string perfil = "principal";

        [Header("Partida")]
        public string faccion = "F1";
        public string codigoParaUnirse = "";

        bool visible;
        bool ocupado;
        float proximoSondeo;
        bool sesionIniciada;
        bool entrarAlCombateAlUnirse;
        string mensaje = "";
        Vector2 scroll;
        readonly PartidaOnlineService servicio = PartidaOnlineService.Instancia;

        void Awake()
        {
            // La escena de batalla también sirve al modo local. Si venimos del botón PvP,
            // detener el flujo local ANTES de que GestorTurnos/MonedaInicial/Temporizador
            // ejecuten Start y avancen fases mientras los jugadores esperan en la sala.
            if (!EntradaDesdeMenuPvP) return;

            var gestor = FindFirstObjectByType<GestorTurnos>();
            if (gestor != null)
            {
                gestor.StopAllCoroutines();
                gestor.enabled = false;
                if (gestor.botonPasarTurno != null) gestor.botonPasarTurno.interactable = false;
            }

            var temporizador = FindFirstObjectByType<Temporizador>();
            if (temporizador != null)
            {
                temporizador.PausarTemporizador();
                temporizador.enabled = false;
            }

            var moneda = FindFirstObjectByType<MonedaInicial>();
            if (moneda != null)
            {
                moneda.StopAllCoroutines();
                moneda.enabled = false;
                if (moneda.monedaVisual != null) moneda.monedaVisual.gameObject.SetActive(false);
                if (moneda.textoMoneda != null) moneda.textoMoneda.text = "";
            }

            foreach (var carta in FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None))
            {
                var boton = carta != null ? carta.GetComponent<UnityEngine.UI.Button>() : null;
                if (boton != null) boton.interactable = false;
            }
        }

        void OnEnable() { servicio.Fallo += AlFallar; }
        void OnDisable() { servicio.Fallo -= AlFallar; }
        void AlFallar(string texto) { mensaje = "ERROR: " + texto; }

        public static void PrepararEntradaDesdeMenu()
        {
            PartidaOnlineService.Instancia.Olvidar();
            EntradaDesdeMenuPvP = true;
        }

        void Start()
        {
            if (!EntradaDesdeMenuPvP) return;
            EntradaDesdeMenuPvP = false;
            entrarAlCombateAlUnirse = true;
            visible = true;
            _ = IniciarSesionConLaCuentaActual();
        }

        async void Update()
        {
            if (!sesionIniciada || ocupado) return;

            // Los push sólo avisan: al recibir uno, se pide la vista privada al servidor (GetMatchState).
            if (servicio.NecesitaResuscribir) await servicio.SuscribirPushAsync();
            if (servicio.HayAvisoPush && servicio.CodigoActual.Length > 0)
            {
                await Ejecutar(servicio.ConsultarAsync());
                return;
            }

            // Respaldo: si un push se pierde (WebSocket caído, app en segundo plano) se consulta igualmente
            // cada ~10 s mientras haya partida activa. Ejecutar() reinicia el temporizador, así que sólo
            // dispara cuando no ha habido ninguna llamada ni aviso en ese tiempo.
            if (servicio.PartidaActiva && Time.unscaledTime >= proximoSondeo)
                await Ejecutar(servicio.ConsultarAsync());
        }

        async System.Threading.Tasks.Task IniciarSesion()
        {
            ocupado = true;
            try
            {
                await UgsDefensa.IniciarAsync(perfil);      // misma sesión anónima que el resto del online
                if (!await servicio.VerificarProtocoloAsync()) { ocupado = false; return; }   // el motivo llega por Fallo
                await servicio.SuscribirPushAsync();
                sesionIniciada = true;
                mensaje = "Sesión iniciada como " + UgsDefensa.MiPlayerId;
                await ReanudarSiHay();
            }
            catch (System.Exception e) { mensaje = "ERROR al iniciar sesión: " + e.Message; }
            ocupado = false;
        }

        async System.Threading.Tasks.Task IniciarSesionConLaCuentaActual()
        {
            ocupado = true;
            try
            {
                // La escena Lab01 ya inició sesión en development. Reutilizamos esa identidad,
                // en vez de cambiarla por el perfil anónimo de las pruebas.
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await ServiciosBootstrap.InicializarAsync("development");

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await UgsDefensa.IniciarAsync(perfil);
                }

                if (!await servicio.VerificarProtocoloAsync()) return;
                await servicio.SuscribirPushAsync();
                sesionIniciada = true;
                mensaje = "Sesión online lista como " + AuthenticationService.Instance.PlayerId;
                // Reconexión: si el juego se cerró a mitad de partida, el servidor la recuerda (GetActiveMatch).
                await ReanudarSiHay();
            }
            catch (System.Exception e) { mensaje = "ERROR al preparar PvP: " + e.Message; }
            finally { ocupado = false; }
        }

        /// <summary>Pregunta al servidor si hay una partida en curso y, si la hay, la retoma (y entra al combate si ya empezó).</summary>
        async System.Threading.Tasks.Task ReanudarSiHay()
        {
            var vista = await servicio.ReanudarActivaAsync();
            if (vista == null || vista.Codigo.Length == 0 || vista.Estado == EstadoPartidaValores.Terminada) return;
            mensaje = vista.Mensaje;
            IntentarEntrarAlCombate();
        }

        async System.Threading.Tasks.Task Ejecutar(System.Threading.Tasks.Task<VistaPartidaDto> llamada)
        {
            ocupado = true;
            proximoSondeo = Time.unscaledTime + PartidaOnlineService.IntervaloSondeoSeg;
            var vista = await llamada;
            if (vista != null)
                mensaje = vista.Resultado == ResultadoPartida.Aplicado || vista.Resultado == ResultadoPartida.Ok
                    ? vista.Mensaje : $"[{vista.Resultado}] {vista.Mensaje}";
            ocupado = false;
            proximoSondeo = Time.unscaledTime + PartidaOnlineService.IntervaloSondeoSeg;
            IntentarEntrarAlCombate();
        }

        void IntentarEntrarAlCombate()
        {
            if (!entrarAlCombateAlUnirse || servicio.Ultima == null
                || servicio.Ultima.Estado != EstadoPartidaValores.EnJuego
                || EscenaOnline.Instancia != null) return;

            string error = EscenaOnline.Activar(servicio);
            if (error.Length > 0)
            {
                mensaje = "ERROR al abrir el combate: " + error;
                return;
            }

            entrarAlCombateAlUnirse = false;
            visible = false;
            mensaje = "Partida conectada. El combate online por turnos comenzó.";
        }

        void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(10, 10, 560, Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("SALA PvP ONLINE  —  autoridad: servidor");
            GUI.enabled = !ocupado;

            if (!sesionIniciada)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("perfil", GUILayout.Width(50));
                perfil = GUILayout.TextField(perfil, GUILayout.Width(120));
                if (GUILayout.Button("Iniciar sesión")) _ = IniciarSesion();
                GUILayout.EndHorizontal();
            }
            else
            {
                DibujarControles();
                DibujarPartida(servicio.Ultima);
            }

            GUI.enabled = true;
            if (mensaje.Length > 0) GUILayout.Label(mensaje);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DibujarControles()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("perfil", GUILayout.Width(40));
            perfil = GUILayout.TextField(perfil, GUILayout.Width(100));
            if (GUILayout.Button("Cambiar perfil", GUILayout.Width(110)))
            {
                servicio.Olvidar();
                sesionIniciada = false;
                _ = IniciarSesion();
            }
            GUILayout.EndHorizontal();

            if (servicio.CodigoActual.Length == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("facción (F1-F4/Random)", GUILayout.Width(150));
                faccion = GUILayout.TextField(faccion, GUILayout.Width(70));
                // Lo normal: el servidor te empareja con quien esté esperando (o te deja esperando a ti).
                if (GUILayout.Button("Buscar rival")) _ = Ejecutar(servicio.BuscarRivalAsync(faccion));
                GUILayout.EndHorizontal();

                GUILayout.Label("…o juega con un amigo con un código de sala:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Crear partida")) _ = Ejecutar(servicio.CrearAsync(faccion));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("código", GUILayout.Width(50));
                codigoParaUnirse = GUILayout.TextField(codigoParaUnirse, 6, GUILayout.Width(90));
                if (GUILayout.Button("Unirse")) _ = Ejecutar(servicio.UnirseAsync(codigoParaUnirse.Trim().ToUpperInvariant(), faccion));
                // Reconexión: con el código de una partida en curso (tuya o no) se recupera su vista.
                if (GUILayout.Button("Reanudar"))
                {
                    servicio.Reanudar(codigoParaUnirse.Trim().ToUpperInvariant());
                    _ = Ejecutar(servicio.ConsultarAsync());
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("CÓDIGO: " + servicio.CodigoActual);
                if (GUILayout.Button("Consultar", GUILayout.Width(80))) _ = Ejecutar(servicio.ConsultarAsync());
                if (GUILayout.Button("Abandonar", GUILayout.Width(80))) _ = Ejecutar(servicio.AbandonarAsync());
                if (GUILayout.Button("Olvidar", GUILayout.Width(70))) servicio.Olvidar();
                GUILayout.EndHorizontal();

                // Dibuja la partida en la escena real (cartas, casillas y peleadores) en vez de en este panel.
                GUILayout.BeginHorizontal();
                if (servicio.Ultima != null && servicio.Ultima.Estado == EstadoPartidaValores.EsperandoRival)
                {
                    GUILayout.Label("Esperando a que se una el otro jugador para abrir el campo de batalla.");
                    if (GUILayout.Button("Cancelar búsqueda", GUILayout.Width(140)))
                        _ = Ejecutar(servicio.CancelarBusquedaAsync());
                }
                else if (EscenaOnline.Instancia == null)
                {
                    if (GUILayout.Button("Jugar en la escena (campo de batalla)"))
                    {
                        string error = EscenaOnline.Activar(servicio);
                        mensaje = error.Length > 0 ? "ERROR: " + error : "Modo escena activado. La sala se cerrará al comenzar el combate.";
                    }
                }
                else GUILayout.Label("Modo escena ACTIVO (recarga la escena para volver al juego local)");
                GUILayout.EndHorizontal();
            }
        }

        void DibujarPartida(VistaPartidaDto v)
        {
            if (v == null) return;

            GUILayout.Space(6);
            GUILayout.Label($"estado: {v.Estado}   fase: {v.Fase}   ronda {v.Ronda}   turno nº {v.NumeroTurno}");
            if (v.Estado == EstadoPartidaValores.EsperandoRival) { GUILayout.Label("Esperando a que el rival se una…"); return; }

            if (v.Estado == EstadoPartidaValores.Terminada)
            {
                var gano = v.GanadorId == v.TuId;
                GUILayout.Label(v.GanadorId.Length == 0 ? $"FIN: empate/cancelada ({v.MotivoFin})"
                    : gano ? $"¡GANASTE! ({v.MotivoFin})" : $"Perdiste ({v.MotivoFin})");
            }
            else
            {
                GUILayout.Label(v.EsTuTurno ? $">>> TU TURNO ({SegundosRestantes(v)} s) <<<" : $"Turno del rival ({SegundosRestantes(v)} s)");
            }

            GUILayout.Label($"tú: {v.MiFaccion}   rival: {v.RivalFaccion} (mano {v.RivalManoCantidad}, preparados {v.RivalPreparados})");
            DibujarCampo("TU CAMPO", v.MiCampo, propio: true, v);
            DibujarCampo("CAMPO RIVAL", v.RivalCampo, propio: false, v);

            if (v.Pregunta != null) DibujarPregunta(v.Pregunta);
            else if (v.EsTuTurno) DibujarAcciones(v);

            if (v.RegistroPelea.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("ÚLTIMA PELEA:");
                var sb = new StringBuilder();
                foreach (var l in v.RegistroPelea) sb.AppendLine(l);
                GUILayout.Label(sb.ToString());
            }
        }

        void DibujarCampo(string titulo, List<PeleadorDto> campo, bool propio, VistaPartidaDto v)
        {
            GUILayout.Label(titulo + (campo.Count == 0 ? ": (vacío)" : ":"));
            foreach (var p in campo)
            {
                var buffs = p.Buffs.Count == 0 ? "" : "  [" + string.Join(", ", p.Buffs.Select(b => $"{b.Tipo}{(b.Valor >= 0 ? "+" : "")}{b.Valor}% x{b.Rondas}")) + "]";
                var prep = propio && p.HabilidadPreparada >= 0 ? $"  (preparada: {p.Habilidades[p.HabilidadPreparada].Nombre})" : "";
                GUILayout.Label($"  casilla {p.Casilla}: {p.Nombre}  {p.Vida}/{p.VidaMax}{buffs}{prep}");
            }
        }

        void DibujarAcciones(VistaPartidaDto v)
        {
            GUILayout.Space(6);
            if (v.Fase == FasePartidaValores.Colocar)
            {
                GUILayout.Label("Coloca UN héroe (o pasa):");
                for (var i = 0; i < v.MiMano.Count; i++)
                    if (GUILayout.Button($"Colocar {v.MiManoNombres[i]}")) _ = Ejecutar(servicio.ColocarAsync(v.MiMano[i]));
            }
            else if (v.Fase == FasePartidaValores.Habilidades)
            {
                GUILayout.Label("Elige una habilidad por peleador (cada una lanza una pregunta):");
                foreach (var p in v.MiCampo.Where(x => x.HabilidadPreparada < 0))
                    foreach (var h in p.Habilidades)
                    {
                        var elegible = p.HabilidadesElegibles.Contains(h.Indice);
                        GUI.enabled = !ocupado && elegible;
                        var etiqueta = $"{p.Nombre}: {h.Nombre}" + (h.Disponible ? (h.Usada ? "  (ya usada)" : "") : "  (aún no implementada)");
                        if (GUILayout.Button(etiqueta)) _ = Ejecutar(servicio.ElegirHabilidadAsync(p.HeroeId, h.Indice));
                        GUI.enabled = !ocupado;
                    }
            }
            if (GUILayout.Button(servicio.HayJugadaPendiente ? "Pasar turno (reintento seguro)" : "Pasar turno"))
                _ = Ejecutar(servicio.PasarTurnoAsync());
        }

        void DibujarPregunta(PreguntaDto q)
        {
            GUILayout.Space(6);
            GUILayout.Label("PREGUNTA: " + q.Texto);
            for (var i = 0; i < q.Opciones.Count; i++)
                if (GUILayout.Button($"{i + 1}. {q.Opciones[i]}")) _ = Ejecutar(servicio.ResponderQuizAsync(q.PreguntaId, i));
        }

        static int SegundosRestantes(VistaPartidaDto v)
        {
            if (string.IsNullOrEmpty(v.DeadlineUtc)) return 0;
            // La hora de referencia es la del servidor; la local sólo se usa para la cuenta atrás visual.
            var limite = System.DateTime.Parse(v.DeadlineUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
            return Mathf.Max(0, (int)(limite - System.DateTime.UtcNow).TotalSeconds);
        }
    }
}
#endif
