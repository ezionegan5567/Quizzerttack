namespace QuizzerMatch;

/// <summary>Valores de EstadoPartida.Estado.</summary>
public static class EstadoValores
{
    public const string EsperandoRival = "esperando_rival";
    public const string EnJuego = "en_juego";
    public const string Terminada = "terminada";
}

/// <summary>Valores de EstadoPartida.Fase (el orden de una ronda es el del GDD y de GestorTurnos).</summary>
public static class FaseValores
{
    public const string Colocar = "colocar";
    public const string Habilidades = "habilidades";
}

/// <summary>
/// Qué pasó con la petición. Las jugadas rechazadas viajan aquí y no como excepción:
/// son parte normal del juego y el cliente necesita distinguirlas.
/// </summary>
public static class Resultado
{
    public const string Ok = "ok";
    public const string Aplicado = "aplicado";
    public const string Repetido = "repetido";
    public const string Obsoleto = "obsoleto";
    public const string NoEsTuTurno = "no_es_tu_turno";
    public const string FaseIncorrecta = "fase_incorrecta";
    public const string Invalido = "invalido";
    public const string Terminada = "terminada";
    public const string Conflicto = "conflicto";
}

public record Resolucion(string Codigo, string Mensaje)
{
    public bool Aplicada => Codigo == Resultado.Aplicado;
    public static Resolucion Ok(string mensaje = "") => new(Resultado.Aplicado, mensaje);
    public static Resolucion Rechazo(string codigo, string mensaje) => new(codigo, mensaje);
}

public class Buff
{
    public string Tipo { get; set; } = "";   // "atk" | "def"
    public int Valor { get; set; }           // porcentaje
    public int Rondas { get; set; }          // rondas que le quedan, contando la actual
}

public class PeleadorEstado
{
    public string HeroeId { get; set; } = "";
    public int Casilla { get; set; }
    public int Vida { get; set; }
    public int VidaMax { get; set; }
    public int HabilidadPreparada { get; set; } = -1;
    public double MultiplicadorPreparado { get; set; } = 1.0;
    /// <summary>Habilidades ya usadas en este ciclo (GDD: no se repiten hasta agotar el repertorio).</summary>
    public List<int> Usadas { get; set; } = new();
    public List<Buff> Buffs { get; set; } = new();

    public bool EstaVivo => Vida > 0;
}

public class JugadorEstado
{
    public string Id { get; set; } = "";
    public string Faccion { get; set; } = "";
    /// <summary>Héroes que aún no se han colocado. Secreto para el rival.</summary>
    public List<string> Mano { get; set; } = new();
    public List<PeleadorEstado> Campo { get; set; } = new();
    public int TimeoutsSeguidos { get; set; }
}

public class PreguntaPendiente
{
    public string JugadorId { get; set; } = "";
    public string PeleadorId { get; set; } = "";
    public int IndiceHabilidad { get; set; }
    public int PreguntaId { get; set; }
    public string VenceUtc { get; set; } = "";
}

/// <summary>
/// El estado de la partida EN MEMORIA. No se guarda: se deduce reproduciendo el RegistroPartida
/// (Motor.Reproducir). Contiene la mano del rival y la semilla, así que nunca se devuelve tal
/// cual al cliente: el cliente recibe una VistaPartida.
/// </summary>
public class EstadoPartida
{
    public string Codigo { get; set; } = "";
    public string Estado { get; set; } = EstadoValores.EsperandoRival;
    public string Fase { get; set; } = "";
    /// <summary>Paso dentro de la fase: 0 = jugador inicial, 1 = el segundo.</summary>
    public int Paso { get; set; }
    /// <summary>Sube con cada cambio de la partida. El cliente lo reenvía como turnoEsperado.</summary>
    public int NumeroTurno { get; set; }
    public int Ronda { get; set; } = 1;

    public string AnfitrionId { get; set; } = "";
    public string InvitadoId { get; set; } = "";
    public string JugadorInicialId { get; set; } = "";
    public string JugadorEnTurnoId { get; set; } = "";
    public string DeadlineUtc { get; set; } = "";

    public int Semilla { get; set; }
    public int RngContador { get; set; }

    public string CreadaPorRequestId { get; set; } = "";

    public Dictionary<string, JugadorEstado> Jugadores { get; set; } = new();
    public PreguntaPendiente? Pregunta { get; set; }
    public List<string> RegistroPelea { get; set; } = new();

    public string GanadorId { get; set; } = "";
    public string MotivoFin { get; set; } = "";

    public string CreadaUtc { get; set; } = "";
    public string ActualizadaUtc { get; set; } = "";

    public string RivalDe(string jugadorId) => jugadorId == AnfitrionId ? InvitadoId : AnfitrionId;
    public bool Participa(string jugadorId) =>
        jugadorId.Length > 0 && (jugadorId == AnfitrionId || jugadorId == InvitadoId);
}
