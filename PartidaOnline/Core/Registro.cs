namespace QuizzerMatch;

/// <summary>
/// Los tipos de jugada. Las fases de la partida (colocar, habilidades, quiz...) no son funciones
/// distintas sino tipos de jugada dentro de la misma lista, como pide el patrón TTTXO: una sola
/// entrada (PlayMove) y un motor que valida la fase, el jugador y los datos.
/// </summary>
public static class TipoJugada
{
    // Las que puede enviar un jugador por PlayMove.
    public const string Colocar = "colocar";
    public const string ElegirHabilidad = "elegir_habilidad";
    public const string ResponderQuiz = "responder_quiz";
    public const string Pasar = "pasar";
    public const string Abandonar = "abandonar";

    // Sólo las escribe el servidor. Un cliente que las envíe por PlayMove es rechazado:
    // si no, podría "vencer" un plazo o colarse como invitado sin pasar por JoinMatch.
    public const string Unirse = "unirse";
    public const string Plazo = "plazo";

    public static readonly HashSet<string> EnviablesPorElJugador = new()
    {
        Colocar, ElegirHabilidad, ResponderQuiz, Pasar, Abandonar,
    };
}

/// <summary>
/// UNA entrada del registro. Lleva la hora del SERVIDOR con la que se aplicó: el motor no lee
/// nunca el reloj, así que reproducir la misma lista da siempre el mismo estado.
/// Un solo tipo con campos opcionales (los que no tocan a la jugada quedan por defecto).
/// </summary>
public class Jugada
{
    public string Tipo { get; set; } = "";
    /// <summary>Quién la hizo. Sale de context.PlayerId; vacío en las jugadas del servidor ("plazo").</summary>
    public string JugadorId { get; set; } = "";
    /// <summary>Clave de idempotencia: un reintento con el mismo requestId no se aplica dos veces.</summary>
    public string RequestId { get; set; } = "";
    public string HoraUtc { get; set; } = "";

    public string Faccion { get; set; } = "";                 // unirse
    public string HeroeId { get; set; } = "";                 // colocar
    public int Casilla { get; set; } = -1;                    // colocar
    public string PeleadorId { get; set; } = "";              // elegir_habilidad
    public int IndiceHabilidad { get; set; }                  // elegir_habilidad
    public int PreguntaId { get; set; }                       // responder_quiz
    public int Opcion { get; set; }                           // responder_quiz

    /// <summary>
    /// Lo que el cliente propone. Sólo se copian los datos de la jugada: quién juega, el requestId y la
    /// hora los pone el servidor, así que nada de eso se puede falsificar desde fuera.
    /// </summary>
    public static Jugada Propuesta(string tipo, string heroeId = "", int casilla = -1, string peleadorId = "",
        int indiceHabilidad = 0, int preguntaId = 0, int opcion = 0) => new()
    {
        Tipo = tipo ?? "",
        HeroeId = heroeId ?? "",
        Casilla = casilla,
        PeleadorId = peleadorId ?? "",
        IndiceHabilidad = indiceHabilidad,
        PreguntaId = preguntaId,
        Opcion = opcion,
    };

    public static Jugada DePlazo(DateTime ahora) => new() { Tipo = TipoJugada.Plazo, HoraUtc = Reglas.Iso(ahora) };

    /// <summary>Copia con la identidad y la hora que decide el servidor.</summary>
    public Jugada Sellada(string jugadorId, string requestId, DateTime ahora) => new()
    {
        Tipo = Tipo, JugadorId = jugadorId, RequestId = requestId, HoraUtc = Reglas.Iso(ahora),
        Faccion = Faccion, HeroeId = HeroeId, Casilla = Casilla, PeleadorId = PeleadorId,
        IndiceHabilidad = IndiceHabilidad, PreguntaId = PreguntaId, Opcion = Opcion,
    };
}

/// <summary>
/// Lo que se guarda en Cloud Save (custom data privado): la cabecera y la lista de jugadas,
/// SOLO DE AGREGAR. No hay foto del estado: el estado se obtiene reproduciendo (Motor.Reproducir).
/// Contiene la semilla y todas las jugadas, incluidas las del rival: nunca sale tal cual al cliente
/// (el cliente recibe una VistaPartida proyectada por jugador).
/// </summary>
public class RegistroPartida
{
    /// <summary>GameProtocol.Version con la que se creó. Ver GameProtocol: las reglas forman parte del contrato.</summary>
    public int Protocolo { get; set; } = GameProtocol.Version;
    public string Codigo { get; set; } = "";
    public string AnfitrionId { get; set; } = "";
    /// <summary>Lo que pidió el anfitrión ("F1".."F4" o "Random"). Random se resuelve con la semilla al reproducir.</summary>
    public string FaccionAnfitrion { get; set; } = "";
    public string CreadaPorRequestId { get; set; } = "";
    /// <summary>Secreta: nunca viaja al cliente (con ella se podría reconstruir la pregunta que toca y el desempate).</summary>
    public int Semilla { get; set; }
    public string CreadaUtc { get; set; } = "";
    public List<Jugada> Jugadas { get; set; } = new();
}
