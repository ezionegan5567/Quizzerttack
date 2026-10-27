// ============ PARTIDA ONLINE POR TURNOS: SERVICIO DE CLIENTE (requiere UGS_ON) ============
// Habla con el módulo Cloud Code "QuizzerMatch" (PartidaOnline/Project) y escucha sus push, con el
// patrón de TTTXO: CreateMatch / JoinMatch / PlayMove / GetMatchState.
//
// Hace TRES cosas y ninguna es "decidir":
//   1. Custodiar los requestId. Una jugada tiene un requestId que debe sobrevivir a todos sus
//      reintentos: si el cliente generara uno nuevo al reintentar, el servidor no sabría que es
//      la misma jugada. Mientras no llegue una respuesta, reintentar la MISMA jugada reutiliza el id
//      (y el mismo turnoEsperado con el que se envió).
//   2. Comprobar la versión del protocolo (GameProtocol.Version) antes de jugar y en cada vista.
//   3. Escuchar los push (servidor -> cliente por WebSocket). El push es sólo un AVISO {type, matchId}
//      ("pasó algo"): al recibirlo se llama a GetMatchState, que devuelve la vista privada. Por eso el
//      push no lleva estado ni información oculta. Si un push se pierde, el panel consulta igualmente
//      cada IntervaloSondeoSeg segundos (sondeo de respaldo) mientras la partida esté activa.
//
// Las llamadas del cliente al servidor NO viajan por el push: son llamadas normales a funciones del
// módulo (CallModuleEndpointAsync). El push sólo funciona en la otra dirección.
// Todas las jugadas (colocar, elegir habilidad, responder el quiz, pasar, abandonar) son UNA función,
// PlayMove, cuyo parámetro `tipo` dice cuál es (TipoJugada en el servidor).

#if UGS_ON
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using UnityEngine;

namespace QuizzerAttack.Partida
{
    public class PartidaOnlineService
    {
        // El nombre del módulo es el del .csproj del servidor (PartidaOnline/Project/QuizzerMatch.csproj).
        const string Modulo = "QuizzerMatch";

        public static readonly PartidaOnlineService Instancia = new PartidaOnlineService();

        /// <summary>Cada cuántos segundos se consulta la partida aunque no llegue ningún push (respaldo, como en TTTXO).</summary>
        public const float IntervaloSondeoSeg = 10f;

        // Tipos de jugada de PlayMove. Deben coincidir con TipoJugada del servidor.
        const string TipoColocar = "colocar";
        const string TipoElegirHabilidad = "elegir_habilidad";
        const string TipoResponderQuiz = "responder_quiz";
        const string TipoPasar = "pasar";
        const string TipoAbandonar = "abandonar";

        public string CodigoActual { get; private set; } = "";
        public VistaPartidaDto Ultima { get; private set; }

        /// <summary>Se dispara con cada vista nueva (respuesta a una jugada, consulta o push).</summary>
        public event Action<VistaPartidaDto> VistaRecibida;
        /// <summary>Se dispara cuando una llamada falla, con un mensaje para la UI.</summary>
        public event Action<string> Fallo;

        // Los callbacks del push pueden llegar fuera del hilo principal: sólo levantan banderas
        // y es el panel (Update) quien actúa.
        volatile bool _hayAviso;
        volatile bool _hayQueResuscribir;

        // Jugada enviada cuyo desenlace no conocemos. Reintentar la misma jugada reutiliza su id.
        // Se guarda también el turno con el que se envió: así el servidor reconoce la jugada por su
        // requestId aunque mientras tanto el estado local haya avanzado.
        string _operacionPendiente = "";
        string _requestIdPendiente = "";
        int _turnoPendiente;

        public bool HayJugadaPendiente => _requestIdPendiente.Length > 0;

        /// <summary>Hay una partida que seguir (esperando rival o en juego): el panel la sondea cada IntervaloSondeoSeg.</summary>
        public bool PartidaActiva =>
            CodigoActual.Length > 0 && Ultima != null && Ultima.Estado != EstadoPartidaValores.Terminada;

        /// <summary>true una sola vez por cada aviso (o cambio de conexión) recibido por push.</summary>
        public bool HayAvisoPush { get { var v = _hayAviso; _hayAviso = false; return v; } }

        /// <summary>true una sola vez tras un Kicked: hay que volver a llamar a SuscribirPushAsync.</summary>
        public bool NecesitaResuscribir { get { var v = _hayQueResuscribir; _hayQueResuscribir = false; return v; } }

        // ---------------------------------------------------------------- partida

        /// <summary>Handshake: false (y Fallo) si el cliente y el servidor no hablan la misma versión del protocolo.</summary>
        public async Task<bool> VerificarProtocoloAsync()
        {
            try
            {
                var servidor = await CloudCodeService.Instance.CallModuleEndpointAsync<int>(Modulo, "GetProtocolVersion", new Dictionary<string, object>());
                if (servidor == GameProtocol.Version) return true;
                Fallo?.Invoke($"Versión del juego incompatible (cliente {GameProtocol.Version}, servidor {servidor}). Actualiza el juego o el módulo.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Partida] GetProtocolVersion falló: {e.Message}");
                Fallo?.Invoke(e.Message);
            }
            return false;
        }

        public Task<VistaPartidaDto> CrearAsync(string faccion) =>
            Llamar("CreateMatch", "crear:" + faccion,
                new Dictionary<string, object> { { "faccion", faccion } },
                conCodigo: false, alRecibir: v => CodigoActual = v.Codigo);

        public Task<VistaPartidaDto> UnirseAsync(string codigo, string faccion) =>
            Llamar("JoinMatch", "unirse:" + codigo,
                new Dictionary<string, object> { { "matchId", codigo }, { "faccion", faccion } },
                conCodigo: false, alRecibir: v => CodigoActual = v.Codigo);

        public Task<VistaPartidaDto> ConsultarAsync() =>
            Llamar("GetMatchState", null, new Dictionary<string, object>(), usaRequestId: false);

        public Task<VistaPartidaDto> ColocarAsync(string heroeId, int casilla = -1) =>
            Mover($"colocar:{heroeId}:{casilla}", TipoColocar, heroeId: heroeId, casilla: casilla);

        public Task<VistaPartidaDto> ElegirHabilidadAsync(string peleadorId, int indice) =>
            Mover($"elegir:{peleadorId}:{indice}", TipoElegirHabilidad, peleadorId: peleadorId, indice: indice);

        public Task<VistaPartidaDto> ResponderQuizAsync(int preguntaId, int opcion) =>
            Mover($"quiz:{preguntaId}:{opcion}", TipoResponderQuiz, preguntaId: preguntaId, opcion: opcion);

        public Task<VistaPartidaDto> PasarTurnoAsync() =>
            Mover("pasar", TipoPasar);

        public Task<VistaPartidaDto> AbandonarAsync() =>
            Mover("abandonar", TipoAbandonar);

        /// <summary>
        /// PlayMove: la única función de juego. Se envían SIEMPRE todos los parámetros (los que no use el
        /// tipo elegido el servidor los ignora): el nombre de cada uno debe coincidir con ModuloPartida.PlayMove.
        /// </summary>
        Task<VistaPartidaDto> Mover(string operacion, string tipo, string heroeId = "", int casilla = -1,
            string peleadorId = "", int indice = 0, int preguntaId = 0, int opcion = 0) =>
            Llamar("PlayMove", operacion, new Dictionary<string, object>
            {
                { "turnoEsperado", TurnoActual },
                { "tipo", tipo },
                { "heroeId", heroeId },
                { "casilla", casilla },
                { "peleadorId", peleadorId },
                { "indiceHabilidad", indice },
                { "preguntaId", preguntaId },
                { "opcion", opcion },
            });

        /// <summary>
        /// Reconexión: tras reiniciar el juego sólo hace falta el código. Fija la partida y el llamador
        /// debe pedir la vista con ConsultarAsync (el servidor reconstruye todo desde su registro).
        /// </summary>
        public void Reanudar(string codigo)
        {
            Olvidar();
            CodigoActual = codigo;
        }

        /// <summary>Olvida la partida en curso (no avisa al servidor: para eso está AbandonarAsync).</summary>
        public void Olvidar()
        {
            CodigoActual = "";
            Ultima = null;
            _operacionPendiente = "";
            _requestIdPendiente = "";
        }

        int TurnoActual => Ultima != null ? Ultima.NumeroTurno : 0;

        // ---------------------------------------------------------------- push

        /// <summary>Suscribe a los mensajes push del jugador. Llamar tras iniciar sesión, y de nuevo si cambia de perfil.</summary>
        public async Task SuscribirPushAsync()
        {
            var callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += @event =>
            {
                // El cuerpo es {type, matchId}. No se decide nada con él: sólo avisa de que hay que consultar.
                Debug.Log($"[Partida][push] {@event.MessageType} {@event.Message}");
                if (@event.MessageType == TipoPush.MatchUpdated) _hayAviso = true;
            };
            callbacks.ConnectionStateChanged += @event =>
            {
                // Mientras estuvimos desconectados pudimos perder avisos: al volver, resincronizar.
                Debug.Log($"[Partida][push] conexión: {JsonConvert.SerializeObject(@event)}");
                _hayAviso = true;
            };
            callbacks.Kicked += () =>
            {
                // El servidor quitó la suscripción (el WebSocket sigue abierto): hay que volver a suscribirse.
                Debug.LogWarning("[Partida][push] Kicked: hay que volver a suscribirse.");
                _hayQueResuscribir = true;
            };
            callbacks.Error += @error => Debug.LogWarning($"[Partida][push] error: {JsonConvert.SerializeObject(@error)}");

            await CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
            Debug.Log("[Partida][push] suscrito a los mensajes del jugador.");
        }

        // ---------------------------------------------------------------- llamada común

        async Task<VistaPartidaDto> Llamar(string funcion, string operacion, Dictionary<string, object> args,
            bool conCodigo = true, bool usaRequestId = true, Action<VistaPartidaDto> alRecibir = null)
        {
            if (conCodigo)
            {
                if (CodigoActual.Length == 0) { Fallo?.Invoke("No hay partida en curso."); return null; }
                args["matchId"] = CodigoActual;
            }

            if (usaRequestId)
            {
                // Mismo gesto sin respuesta => mismo requestId (y mismo turno). Gesto distinto => id nuevo.
                if (_operacionPendiente != operacion || _requestIdPendiente.Length == 0)
                {
                    _operacionPendiente = operacion;
                    _requestIdPendiente = Guid.NewGuid().ToString("N");
                    _turnoPendiente = args.TryGetValue("turnoEsperado", out var turno) ? (int)turno : 0;
                }
                args["requestId"] = _requestIdPendiente;
                if (args.ContainsKey("turnoEsperado")) args["turnoEsperado"] = _turnoPendiente;
            }

            try
            {
                var vista = await CloudCodeService.Instance.CallModuleEndpointAsync<VistaPartidaDto>(Modulo, funcion, args);

                // Llegó una respuesta (aunque sea un rechazo): el desenlace ya se conoce.
                if (usaRequestId) { _operacionPendiente = ""; _requestIdPendiente = ""; }
                if (vista == null) return null;

                // Otra versión del contrato: no se pinta nada de lo que llegue.
                if (vista.Protocolo != GameProtocol.Version)
                {
                    Fallo?.Invoke($"Versión del juego incompatible (cliente {GameProtocol.Version}, servidor {vista.Protocolo}).");
                    return null;
                }

                // Una respuesta atrasada (p. ej. un reintento) no debe retroceder lo que ya sabemos.
                if (Ultima == null || vista.Codigo != Ultima.Codigo || vista.NumeroTurno >= Ultima.NumeroTurno) Ultima = vista;
                alRecibir?.Invoke(vista);
                VistaRecibida?.Invoke(Ultima);
                return vista;
            }
            catch (CloudCodeException e)
            {
                // Sin respuesta: _requestIdPendiente se conserva, y reintentar es seguro.
                Debug.LogWarning($"[Partida] {funcion} falló: {e.Reason} ({e.ErrorCode}) {e.Message}");
                Fallo?.Invoke(e.Message);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Partida] {funcion} falló: {e.Message}");
                Fallo?.Invoke(e.Message);
                return null;
            }
        }
    }
}
#endif
