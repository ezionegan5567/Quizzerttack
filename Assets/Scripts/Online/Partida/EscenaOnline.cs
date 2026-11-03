// ============ MODO ONLINE EN LA ESCENA REAL "campo de batalla" (requiere UGS_ON) ============
// Hace que la escena de siempre (cartas, casillas, peleadores) se dibuje desde la VistaPartidaDto del servidor
// en vez de dejar que GestorTurnos decida. Es ADITIVO: no hay que tocar la escena en Unity. Se crea en tiempo de
// ejecución con el botón "Jugar en la escena" del panel F7 y, mientras no se active, el juego local funciona igual.
//
// Qué hace:
//   - Silencia lo local: GestorTurnos (fases), Temporizador (30 s) y MonedaInicial (moneda al azar local).
//   - Cada vista que llega (respuesta, push o sondeo): coloca los héroes que el servidor dice, actualiza la vida,
//     quita los caídos, activa sólo las cartas que se pueden usar y pinta el turno, el plazo y la última pelea.
//   - Los clics (carta, habilidad, pasar) NO se resuelven aquí: se envían al servidor, que valida y contesta.
//   - La pregunta del quiz sale del servidor (sin la respuesta correcta) y se responde en una ventana propia.
//
// Qué NO hace (a propósito, para que lo hagan ustedes: ver docs/TAREAS.md, Tarea 3):
//   - Animaciones: la pelea llega ya resuelta y se muestra como texto; la vida cambia de golpe.
//   - Efectos de estado (buffs, veneno...): el servidor los calcula pero esta escena no los dibuja.
//   - Más facciones que F1 y F2: la escena sólo tiene esas dos manos (ver EscenaLogica.ResolverLados).
// Para volver al juego local, recarguen la escena.

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace QuizzerAttack.Partida
{
    public class EscenaOnline : MonoBehaviour
    {
        const float IntervaloSincronizacion = 0.25f;
        const float SegundosMensaje = 6f;

        public static EscenaOnline Instancia { get; private set; }

        PartidaOnlineService servicio;
        GestorTurnos gestor;
        Temporizador temporizador;
        VistaPartidaDto vista;

        bool ladosListos;
        int ladoMio, ladoRival;
        string errorLados = "";

        bool ocupado;
        bool sucia;
        float proximaSincronizacion;
        string mensaje = "";
        float mensajeHasta;
        readonly HashSet<string> avisados = new HashSet<string>();

        /// <summary>Activa el modo online en la escena actual. Devuelve un texto de error, o "" si salió bien.</summary>
        public static string Activar(PartidaOnlineService servicio)
        {
            if (Instancia != null) return "";
            GestorTurnos gestor = UnityEngine.Object.FindFirstObjectByType<GestorTurnos>();
            if (gestor == null) return "Esta no es la escena del campo de batalla (no hay GestorTurnos).";
            if (servicio.CodigoActual.Length == 0) return "Crea o únete a una partida antes de jugar en la escena.";

            GameObject go = new GameObject("EscenaOnline");
            go.AddComponent<EscenaOnline>().Iniciar(servicio, gestor);
            return "";
        }

        void Iniciar(PartidaOnlineService s, GestorTurnos g)
        {
            Instancia = this;
            servicio = s;
            gestor = g;

            // 1. Silenciar lo local para que no decida nada.
            gestor.enabled = false;
            temporizador = gestor.temporizador != null ? gestor.temporizador : UnityEngine.Object.FindFirstObjectByType<Temporizador>();
            if (temporizador != null) temporizador.PausarTemporizador();

            MonedaInicial moneda = UnityEngine.Object.FindFirstObjectByType<MonedaInicial>();
            if (moneda != null)
            {
                moneda.StopAllCoroutines();   // la moneda la sortea el servidor
                moneda.enabled = false;
                if (moneda.monedaVisual != null) moneda.monedaVisual.gameObject.SetActive(false);
                if (moneda.textoMoneda != null) moneda.textoMoneda.text = "";
            }

            // 2. Empezar de cero: sin peleadores ni cartas usadas de una partida local anterior.
            Peleador.LimpiarEstaticos();
            foreach (Peleador p in UnityEngine.Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None))
                if (p != null) Destroy(p.gameObject);
            foreach (CartaSeleccion c in UnityEngine.Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None))
                if (c != null) c.ReiniciarCarta();

            // 3. "Pasar turno" ya no avanza fases locales: se lo pide al servidor.
            if (gestor.botonPasarTurno != null)
            {
                gestor.botonPasarTurno.onClick.RemoveAllListeners();
                gestor.botonPasarTurno.onClick.AddListener(() => { _ = Ejecutar(() => servicio.PasarTurnoAsync()); });
                gestor.botonPasarTurno.interactable = false;
            }

            // 4. Ganchos de Peleador y CartaSeleccion.
            ModoOnline.PuedeElegirHabilidad = (dueno, nombre) =>
                ladosListos && dueno == ladoMio && EscenaLogica.PuedeElegirHabilidad(vista, EscenaLogica.NombreAId(nombre));
            ModoOnline.AlClicCarta = AlClicCarta;
            ModoOnline.AlElegirHabilidad = AlElegirHabilidad;
            ModoOnline.Activo = true;

            servicio.VistaRecibida += AlRecibirVista;
            servicio.Fallo += AlFallar;

            if (servicio.Ultima != null) AlRecibirVista(servicio.Ultima);
            else _ = Ejecutar(() => servicio.ConsultarAsync());
            Decir("Modo online activado. Recarga la escena para volver al juego local.");
        }

        void OnDestroy()
        {
            if (servicio != null)
            {
                servicio.VistaRecibida -= AlRecibirVista;
                servicio.Fallo -= AlFallar;
            }
            ModoOnline.Desactivar();
            if (Instancia == this) Instancia = null;
        }

        // ---------------------------------------------------------------- eventos

        void AlRecibirVista(VistaPartidaDto v)
        {
            vista = v;
            sucia = true;
        }

        void AlFallar(string texto) { Decir("ERROR: " + texto); }

        void AlClicCarta(int dueno, string nombre)
        {
            if (vista == null || vista.Estado == EstadoPartidaValores.EsperandoRival) { Decir("Espera a que se una el rival."); return; }
            if (!ladosListos) { Decir(errorLados); return; }
            if (dueno != ladoMio) { Decir("Esa carta es del rival."); return; }
            // No se valida turno ni fase aquí: se envía y el servidor contesta con el motivo si no se puede.
            string id = EscenaLogica.NombreAId(nombre);
            _ = Ejecutar(() => servicio.ColocarAsync(id));
        }

        void AlElegirHabilidad(int dueno, string nombre, int indice)
        {
            if (!ladosListos || dueno != ladoMio) return;
            string id = EscenaLogica.NombreAId(nombre);
            _ = Ejecutar(() => servicio.ElegirHabilidadAsync(id, indice));
        }

        /// <summary>Envía una jugada al servidor (de una en una) y muestra por qué se rechazó, si se rechazó.</summary>
        async Task Ejecutar(Func<Task<VistaPartidaDto>> llamada)
        {
            if (ocupado) return;
            ocupado = true;
            try
            {
                VistaPartidaDto v = await llamada();
                if (v != null && v.Resultado != ResultadoPartida.Aplicado && v.Resultado != ResultadoPartida.Ok)
                    Decir("[" + v.Resultado + "] " + v.Mensaje);
                else if (v != null && v.Mensaje.Length > 0)
                    Decir(v.Mensaje);
            }
            finally { ocupado = false; }
        }

        // ---------------------------------------------------------------- sincronización

        void Update()
        {
            if (vista == null) return;
            if (sucia || Time.unscaledTime >= proximaSincronizacion)
            {
                sucia = false;
                proximaSincronizacion = Time.unscaledTime + IntervaloSincronizacion;
                Sincronizar();
            }
            PintarPlazo();
        }

        void Sincronizar()
        {
            // Los lados se resuelven cuando los dos jugadores ya están (antes no se conoce la facción del rival).
            if (!ladosListos && vista.Estado != EstadoPartidaValores.EsperandoRival)
            {
                List<EscenaLogica.CartaEscena> cartas = UnityEngine.Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None)
                    .Select(c => new EscenaLogica.CartaEscena { Faccion = c.faccion, JugadorDueno = c.jugadorDueno }).ToList();
                ladosListos = EscenaLogica.ResolverLados(vista.MiFaccion, vista.RivalFaccion, cartas, out ladoMio, out ladoRival, out errorLados);
            }

            PintarTextos();
            if (!ladosListos) return;

            Peleador[] peleadores = UnityEngine.Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None);
            List<EscenaLogica.PeleadorEscena> escena = peleadores.Select(p => new EscenaLogica.PeleadorEscena
            {
                Lado = p.jugadorDueno,
                HeroeId = EscenaLogica.NombreAId(p.nombrePeleador),
                Casilla = p.indiceCasilla,
                Vida = p.vida,
                VidaMax = p.vidaMax,
                Preparada = p.HabilidadLista(),
            }).ToList();

            foreach (EscenaLogica.Accion a in EscenaLogica.Planificar(vista, ladoMio, ladoRival, escena))
                Aplicar(a, peleadores);

            PintarCartasYBoton();
        }

        void Aplicar(EscenaLogica.Accion a, Peleador[] peleadores)
        {
            switch (a.Tipo)
            {
                case EscenaLogica.TipoAccion.Colocar:
                    CartaSeleccion carta = UnityEngine.Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None).FirstOrDefault(c =>
                        c.jugadorDueno == a.Lado && EscenaLogica.NombreAId(c.nombreHeroe) == a.HeroeId && !c.EstaUsada());
                    if (carta == null) { AvisarUnaVez("No hay carta para '" + a.HeroeId + "' en el lado " + a.Lado); break; }
                    if (carta.ColocarDesdeServidor(a.Casilla)) sucia = true;   // Peleador.Start reinicia la vida: se vuelve a sincronizar al frame siguiente
                    else AvisarUnaVez("No se pudo colocar '" + a.HeroeId + "' en la casilla " + a.Casilla);
                    break;

                case EscenaLogica.TipoAccion.Actualizar:
                    Peleador p = peleadores.FirstOrDefault(x => x != null && x.jugadorDueno == a.Lado && EscenaLogica.NombreAId(x.nombrePeleador) == a.HeroeId);
                    if (p != null) p.AplicarVistaOnline(a.Vida, a.VidaMax, a.Preparada);
                    break;

                case EscenaLogica.TipoAccion.Quitar:
                    Peleador caido = peleadores.FirstOrDefault(x => x != null && x.jugadorDueno == a.Lado && EscenaLogica.NombreAId(x.nombrePeleador) == a.HeroeId);
                    if (caido != null) Destroy(caido.gameObject);
                    break;
            }
        }

        void AvisarUnaVez(string texto)
        {
            if (avisados.Add(texto)) Debug.LogWarning("[EscenaOnline] " + texto);
        }

        // ---------------------------------------------------------------- pintar

        void PintarCartasYBoton()
        {
            foreach (CartaSeleccion c in UnityEngine.Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None))
            {
                Button b = c != null ? c.GetComponent<Button>() : null;
                if (b == null) continue;
                // Sólo es una ayuda visual: aunque se pudiera hacer clic, el servidor volvería a comprobarlo todo.
                b.interactable = !c.EstaUsada() && c.jugadorDueno == ladoMio
                    && EscenaLogica.PuedeColocar(vista, EscenaLogica.NombreAId(c.nombreHeroe));
            }
            if (gestor.botonPasarTurno != null)
                gestor.botonPasarTurno.interactable = EscenaLogica.PuedePasar(vista) && !ocupado;
        }

        void PintarTextos()
        {
            if (gestor.textoTurno != null)
                gestor.textoTurno.text = !ladosListos && errorLados.Length > 0 && vista.Estado != EstadoPartidaValores.EsperandoRival
                    ? errorLados : EscenaLogica.TextoTurno(vista);

            if (gestor.textoResultado != null)
                gestor.textoResultado.text = vista.RegistroPelea.Count == 0 ? ""
                    : "ÚLTIMA PELEA:\n" + string.Join("\n", vista.RegistroPelea.Skip(Math.Max(0, vista.RegistroPelea.Count - 8)));
        }

        void PintarPlazo()
        {
            if (temporizador == null || temporizador.textoTemporizador == null) return;
            temporizador.PausarTemporizador();   // por si algo local lo reanudó
            int s = vista.Estado == EstadoPartidaValores.EnJuego ? EscenaLogica.SegundosRestantes(vista, DateTime.UtcNow) : 0;
            temporizador.textoTemporizador.text = string.Format("tiempo: {0:00}:{1:00}", s / 60, s % 60);
        }

        void Decir(string texto)
        {
            mensaje = texto;
            mensajeHasta = Time.unscaledTime + SegundosMensaje;
        }

        // ---------------------------------------------------------------- IMGUI: pregunta y mensajes

        void OnGUI()
        {
            if (Time.unscaledTime < mensajeHasta && mensaje.Length > 0)
                GUI.Box(new Rect(Screen.width - 430, 10, 420, 70), mensaje);

            if (vista == null || vista.Pregunta == null) return;

            PreguntaDto q = vista.Pregunta;
            int quedan = EscenaLogica.SegundosHasta(q.VenceUtc, DateTime.UtcNow);
            Rect r = new Rect(Screen.width / 2f - 280, Screen.height / 2f - 170, 560, 340);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.Label("PREGUNTA  (" + quedan + " s)");
            GUILayout.Label(q.Texto);
            GUILayout.Space(8);
            GUI.enabled = !ocupado;
            for (int i = 0; i < q.Opciones.Count; i++)
            {
                int opcion = i;
                int preguntaId = q.PreguntaId;
                if (GUILayout.Button((i + 1) + ". " + q.Opciones[i], GUILayout.Height(40)))
                    _ = Ejecutar(() => servicio.ResponderQuizAsync(preguntaId, opcion));
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
#endif
