namespace QuizzerMatch;

/// <summary>Un jugador esperando rival: su sala ya existe y está en "esperando_rival".</summary>
public class EntradaCola
{
    public string JugadorId { get; set; } = "";
    public string MatchId { get; set; } = "";
    /// <summary>Hora del servidor en que se anotó (o se refrescó). Sirve para caducar a quien cerró el juego.</summary>
    public string DesdeUtc { get; set; } = "";
}

/// <summary>
/// La cola de "Buscar rival". Es UN documento en Cloud Save (custom data privado), escrito con write lock:
/// si dos búsquedas llegan a la vez, una pierde la escritura y reintenta, así que nunca se reclama dos veces
/// al mismo rival. Para una clase o un evento pequeño alcanza; con miles de jugadores a la vez el documento
/// único se vuelve un cuello de botella y el siguiente escalón es el Matchmaker de UGS (como en TTTXO).
/// </summary>
public class ColaEmparejamiento
{
    public List<EntradaCola> Entradas { get; set; } = new();
}

/// <summary>
/// Las decisiones de la cola, en C# puro y sin red (se prueban con pruebas normales). El servicio sólo carga,
/// llama a esto y guarda.
/// </summary>
public static class Emparejamiento
{
    /// <summary>Quien lleva más de esto sin que nadie lo empareje (cerró el juego, perdió la conexión) sale de la cola.</summary>
    public const int SegundosEnCola = 120;

    /// <summary>Quita las entradas caducadas. Devuelve cuántas quitó.</summary>
    public static int Podar(ColaEmparejamiento c, DateTime ahora) =>
        c.Entradas.RemoveAll(e => e.DesdeUtc.Length == 0 || (ahora - Reglas.Parse(e.DesdeUtc)).TotalSeconds > SegundosEnCola);

    /// <summary>El rival que toca: el que más tiempo lleva esperando, y nunca uno mismo.</summary>
    public static EntradaCola? Candidato(ColaEmparejamiento c, string jugadorId) =>
        c.Entradas.Where(e => e.JugadorId != jugadorId).OrderBy(e => Reglas.Parse(e.DesdeUtc)).FirstOrDefault();

    /// <summary>Anota (o refresca) al jugador con su sala. Un jugador aparece como mucho una vez.</summary>
    public static void Anotar(ColaEmparejamiento c, string jugadorId, string matchId, DateTime ahora)
    {
        Quitar(c, jugadorId);
        c.Entradas.Add(new EntradaCola { JugadorId = jugadorId, MatchId = matchId, DesdeUtc = Reglas.Iso(ahora) });
    }

    public static bool Quitar(ColaEmparejamiento c, string jugadorId) => c.Entradas.RemoveAll(e => e.JugadorId == jugadorId) > 0;
}
