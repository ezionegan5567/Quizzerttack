namespace QuizzerMatch;

public class BuffVista
{
    public string Tipo { get; set; } = "";
    public int Valor { get; set; }
    public int Rondas { get; set; }
}

/// <summary>Datos públicos de una habilidad (el nombre y si el servidor la soporta ya).</summary>
public class HabilidadVista
{
    public int Indice { get; set; }
    public string Nombre { get; set; } = "";
    public string Efecto { get; set; } = "";
    public bool Disponible { get; set; }
    public bool Usada { get; set; }
}

public class PeleadorVista
{
    public string HeroeId { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Casilla { get; set; }
    public int Vida { get; set; }
    public int VidaMax { get; set; }
    /// <summary>Sólo en tus peleadores. En los del rival siempre es -1: la jugada se revela en la pelea.</summary>
    public int HabilidadPreparada { get; set; } = -1;
    /// <summary>Sólo en tus peleadores: índices de habilidades que puedes elegir ahora.</summary>
    public List<int> HabilidadesElegibles { get; set; } = new();
    /// <summary>Sólo en tus peleadores: las 4 habilidades con su estado, para pintar los botones.</summary>
    public List<HabilidadVista> Habilidades { get; set; } = new();
    public List<BuffVista> Buffs { get; set; } = new();
}

/// <summary>Texto y opciones. El índice correcto no viaja nunca.</summary>
public class PreguntaVista
{
    public int PreguntaId { get; set; }
    public string Texto { get; set; } = "";
    public List<string> Opciones { get; set; } = new();
    public string PeleadorId { get; set; } = "";
    public int IndiceHabilidad { get; set; }
    public string VenceUtc { get; set; } = "";
}

/// <summary>
/// Lo que ve UN jugador. Es el único objeto que sale del servidor hacia el cliente:
/// no contiene la mano del rival, ni sus habilidades preparadas, ni la semilla, ni la
/// respuesta correcta de las preguntas.
/// </summary>
public class VistaPartida
{
    /// <summary>GameProtocol.Version del servidor: el cliente se niega a pintar si no coincide con la suya.</summary>
    public int Protocolo { get; set; }
    public string Codigo { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Fase { get; set; } = "";
    public int NumeroTurno { get; set; }
    public int Ronda { get; set; }

    public string TuId { get; set; } = "";
    public string RivalId { get; set; } = "";
    public string JugadorEnTurnoId { get; set; } = "";
    /// <summary>Lo decide el servidor. El cliente sólo lo obedece.</summary>
    public bool EsTuTurno { get; set; }
    public string DeadlineUtc { get; set; } = "";

    public string MiFaccion { get; set; } = "";
    public string RivalFaccion { get; set; } = "";
    public List<string> MiMano { get; set; } = new();
    /// <summary>Los nombres de MiMano, en el mismo orden (para pintar las cartas).</summary>
    public List<string> MiManoNombres { get; set; } = new();
    public int RivalManoCantidad { get; set; }
    public List<PeleadorVista> MiCampo { get; set; } = new();
    public List<PeleadorVista> RivalCampo { get; set; } = new();
    public int RivalPreparados { get; set; }

    public PreguntaVista? Pregunta { get; set; }
    public List<string> RegistroPelea { get; set; } = new();

    public string GanadorId { get; set; } = "";
    public string MotivoFin { get; set; } = "";

    /// <summary>Qué pasó con la petición. Ver <see cref="QuizzerMatch.Resultado"/>.</summary>
    public string Resultado { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public string ServerTimeUtc { get; set; } = "";
}

public static class Vistas
{
    public static VistaPartida Construir(EstadoPartida s, string jugadorId, string resultado, string mensaje, DateTime ahora)
    {
        var rivalId = s.RivalDe(jugadorId);
        s.Jugadores.TryGetValue(jugadorId, out var yo);
        s.Jugadores.TryGetValue(rivalId, out var rival);

        var vista = new VistaPartida
        {
            Protocolo = GameProtocol.Version,
            Codigo = s.Codigo,
            Estado = s.Estado,
            Fase = s.Fase,
            NumeroTurno = s.NumeroTurno,
            Ronda = s.Ronda,
            TuId = jugadorId,
            RivalId = rivalId,
            JugadorEnTurnoId = s.JugadorEnTurnoId,
            EsTuTurno = s.Estado == EstadoValores.EnJuego && s.JugadorEnTurnoId == jugadorId,
            DeadlineUtc = s.DeadlineUtc,
            MiFaccion = yo?.Faccion ?? "",
            RivalFaccion = rival?.Faccion ?? "",
            MiMano = yo?.Mano.ToList() ?? new List<string>(),
            MiManoNombres = yo?.Mano.Select(id => Catalogo.Heroe(id).Nombre).ToList() ?? new List<string>(),
            RivalManoCantidad = rival?.Mano.Count ?? 0,
            MiCampo = yo?.Campo.OrderBy(p => p.Casilla).Select(p => DePeleador(p, propio: true)).ToList() ?? new(),
            RivalCampo = rival?.Campo.OrderBy(p => p.Casilla).Select(p => DePeleador(p, propio: false)).ToList() ?? new(),
            RivalPreparados = rival?.Campo.Count(p => p.HabilidadPreparada >= 0) ?? 0,
            RegistroPelea = s.RegistroPelea.ToList(),
            GanadorId = s.GanadorId,
            MotivoFin = s.MotivoFin,
            Resultado = resultado,
            Mensaje = mensaje,
            ServerTimeUtc = Reglas.Iso(ahora),
        };

        // La pregunta sólo se le enseña a quien debe contestarla, y sin la respuesta.
        if (s.Pregunta != null && s.Pregunta.JugadorId == jugadorId)
        {
            var def = BancoPreguntas.Obtener(s.Pregunta.PreguntaId);
            vista.Pregunta = new PreguntaVista
            {
                PreguntaId = def.Id,
                Texto = def.Texto,
                Opciones = def.Opciones.ToList(),
                PeleadorId = s.Pregunta.PeleadorId,
                IndiceHabilidad = s.Pregunta.IndiceHabilidad,
                VenceUtc = s.Pregunta.VenceUtc,
            };
        }
        return vista;
    }

    private static PeleadorVista DePeleador(PeleadorEstado p, bool propio) => new()
    {
        HeroeId = p.HeroeId,
        Nombre = Catalogo.Heroe(p.HeroeId).Nombre,
        Casilla = p.Casilla,
        Vida = p.Vida,
        VidaMax = p.VidaMax,
        HabilidadPreparada = propio ? p.HabilidadPreparada : -1,
        HabilidadesElegibles = propio && p.EstaVivo && p.HabilidadPreparada < 0 ? Reglas.Elegibles(p).ToList() : new List<int>(),
        Habilidades = propio
            ? Catalogo.Heroe(p.HeroeId).Habilidades.Select((h, i) => new HabilidadVista
              {
                  Indice = i, Nombre = h.Nombre, Efecto = h.Efecto.ToString(),
                  Disponible = Reglas.Disponible(h), Usada = p.Usadas.Contains(i),
              }).ToList()
            : new List<HabilidadVista>(),
        Buffs = p.Buffs.Select(b => new BuffVista { Tipo = b.Tipo, Valor = b.Valor, Rondas = b.Rondas }).ToList(),
    };
}
