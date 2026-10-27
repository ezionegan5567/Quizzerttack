// ============ PARTIDA ONLINE POR TURNOS: PANEL DE PRUEBA (requiere UGS_ON) ============
// Panel IMGUI (tecla F7) para jugar la partida online completa sin tocar el resto de la escena:
// crear/unirse, colocar, elegir habilidad, responder el quiz, pasar y abandonar. Dibuja SOLO lo que
// dice la vista del servidor y no decide nada. Se actualiza por push (aviso {type, matchId} -> GetMatchState)
// con un sondeo de respaldo cada ~10 s.
//
// Montaje: en el GameObject "Registros" (o cualquiera) -> Add Component -> PanelPartidaOnline.
// Para probar con UN solo Editor: usa dos perfiles (campo "perfil" + botón "Cambiar perfil"); cada
// perfil es un jugador anónimo distinto. Con dos Editores (o ParrelSync) cada uno usa su perfil por defecto.
//
// SIGUIENTE PASO (para el equipo): sustituir este panel por la escena real, haciendo que GestorTurnos,
// CartaSeleccion y Peleador PINTEN la VistaPartidaDto en vez de decidir fases y peleas en local.

#if UGS_ON
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace QuizzerAttack.Partida
{
    public class PanelPartidaOnline : MonoBehaviour
    {
        [Header("Perfil (para jugar los dos lados desde un solo Editor)")]
        public string perfil = "principal";

        [Header("Partida")]
        public string faccion = "F1";
        public string codigoParaUnirse = "";

        bool visible;
        bool ocupado;
        float proximoSondeo;
        bool sesionIniciada;
        string mensaje = "";
        Vector2 scroll;
        readonly PartidaOnlineService servicio = PartidaOnlineService.Instancia;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool TeclaAbrir() { var kb = Keyboard.current; return kb != null && kb.f7Key.wasPressedThisFrame; }
#else
        bool TeclaAbrir() { return Input.GetKeyDown(KeyCode.F7); }
#endif

        void OnEnable() { servicio.Fallo += AlFallar; }
        void OnDisable() { servicio.Fallo -= AlFallar; }
        void AlFallar(string texto) { mensaje = "ERROR: " + texto; }

        async void Update()
        {
            if (TeclaAbrir()) visible = !visible;
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
            }
            catch (System.Exception e) { mensaje = "ERROR al iniciar sesión: " + e.Message; }
            ocupado = false;
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
        }

        void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(10, 10, 560, Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("PARTIDA ONLINE (F7)  —  autoridad: Cloud Code · avisos: push");
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
                if (GUILayout.Button("Crear partida")) _ = Ejecutar(servicio.CrearAsync(faccion));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("código", GUILayout.Width(50));
                codigoParaUnirse = GUILayout.TextField(codigoParaUnirse, 6, GUILayout.Width(90));
                if (GUILayout.Button("Unirse")) _ = Ejecutar(servicio.UnirseAsync(codigoParaUnirse.Trim().ToUpperInvariant(), faccion));
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
