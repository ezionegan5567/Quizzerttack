using Newtonsoft.Json.Linq;
using QuizzerMatch;

namespace QuizzerMatch.Tests;

/// <summary>
/// Cloud Save en memoria. Guarda JSON (no objetos) para que cada prueba pase por la misma
/// serialización que en producción, y simula el write lock: guardar con un lock viejo falla.
/// </summary>
public class RepositorioMemoria : IRepositorioPartida
{
    private readonly Dictionary<string, (JObject Json, int Version)> _datos = new();

    /// <summary>Hace que el próximo Guardar pierda la carrera, como si otro jugador hubiera escrito antes.</summary>
    public bool FallarProximoGuardado { get; set; }
    public int Guardados { get; private set; }

    public Task<PartidaGuardada?> CargarAsync(string codigo)
    {
        if (!_datos.TryGetValue(codigo, out var d)) return Task.FromResult<PartidaGuardada?>(null);
        return Task.FromResult<PartidaGuardada?>(new PartidaGuardada(d.Json.ToObject<RegistroPartida>()!, d.Version.ToString()));
    }

    public Task<bool> GuardarAsync(RegistroPartida registro, string? writeLock)
    {
        if (FallarProximoGuardado) { FallarProximoGuardado = false; return Task.FromResult(false); }
        if (_datos.TryGetValue(registro.Codigo, out var actual) && writeLock != null && writeLock != actual.Version.ToString())
            return Task.FromResult(false);

        _datos[registro.Codigo] = (JObject.FromObject(registro), (_datos.TryGetValue(registro.Codigo, out actual) ? actual.Version : 0) + 1);
        Guardados++;
        return Task.FromResult(true);
    }

    /// <summary>Lo que hay guardado: la cabecera y la lista de jugadas.</summary>
    public RegistroPartida Registro(string codigo) => _datos[codigo].Json.ToObject<RegistroPartida>()!;

    /// <summary>El estado, deducido reproduciendo lo guardado (el estado no se guarda).</summary>
    public EstadoPartida Estado(string codigo) => Motor.Reproducir(Registro(codigo));

    /// <summary>Sobrescribe el registro tal cual (para simular un registro manipulado o de otra versión).</summary>
    public void Poner(RegistroPartida registro) =>
        _datos[registro.Codigo] = (JObject.FromObject(registro), (_datos.TryGetValue(registro.Codigo, out var a) ? a.Version : 0) + 1);

    public int Cantidad => _datos.Count;
}

public record Aviso(string JugadorId, string MatchId);

public class NotificadorEspia : INotificador
{
    public List<Aviso> Avisos { get; } = new();

    public Task NotificarAsync(string jugadorId, string matchId)
    {
        Avisos.Add(new Aviso(jugadorId, matchId));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Una partida lista para jugar entre "A" (anfitrión) y "B" (invitado), con reloj controlable.
/// Habla con el servicio como lo haría el módulo: sólo CreateMatch, JoinMatch, PlayMove y GetMatchState.
/// </summary>
public class Escenario
{
    public const string A = "jugador-A";
    public const string B = "jugador-B";

    public RepositorioMemoria Repo { get; } = new();
    public NotificadorEspia Avisos { get; } = new();
    public ServicioPartida Servicio { get; }
    public DateTime Ahora { get; set; } = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    public string Codigo { get; private set; } = "";
    private int _peticion;

    public Escenario(int semilla = 42, string faccionA = "F1", string faccionB = "F2")
    {
        Servicio = new ServicioPartida(Repo, Avisos, () => semilla);
        FaccionA = faccionA;
        FaccionB = faccionB;
    }

    public string FaccionA { get; }
    public string FaccionB { get; }

    /// <summary>Un requestId nuevo por jugada (el cliente real reutiliza el mismo sólo al reintentar).</summary>
    public string NuevaPeticion() => $"req-{++_peticion}";

    public async Task<VistaPartida> Empezar()
    {
        var creada = await Servicio.CreateMatchAsync(A, NuevaPeticion(), FaccionA, Ahora);
        Codigo = creada.Codigo;
        return await Servicio.JoinMatchAsync(B, Codigo, NuevaPeticion(), FaccionB, Ahora);
    }

    public EstadoPartida Estado => Repo.Estado(Codigo);
    public RegistroPartida Registro => Repo.Registro(Codigo);

    public Task<VistaPartida> Ver(string jugador) => Servicio.GetMatchStateAsync(jugador, Codigo, Ahora);

    public string EnTurno => Estado.JugadorEnTurnoId;
    public string Otro(string jugador) => jugador == A ? B : A;

    /// <summary>PlayMove con requestId nuevo y el turno esperado actual (lo que hace un cliente al día).</summary>
    public Task<VistaPartida> Mover(string jugador, Jugada propuesta) =>
        Servicio.PlayMoveAsync(jugador, Codigo, NuevaPeticion(), Estado.NumeroTurno, propuesta, Ahora);

    public Task<VistaPartida> Colocar(string jugador, string heroe, int casilla = -1) =>
        Mover(jugador, Jugada.Propuesta(TipoJugada.Colocar, heroeId: heroe, casilla: casilla));

    public Task<VistaPartida> Pasar(string jugador) =>
        Mover(jugador, Jugada.Propuesta(TipoJugada.Pasar));

    public Task<VistaPartida> Elegir(string jugador, string peleador, int indice) =>
        Mover(jugador, Jugada.Propuesta(TipoJugada.ElegirHabilidad, peleadorId: peleador, indiceHabilidad: indice));

    public Task<VistaPartida> Responder(string jugador, int preguntaId, int opcion) =>
        Mover(jugador, Jugada.Propuesta(TipoJugada.ResponderQuiz, preguntaId: preguntaId, opcion: opcion));

    public Task<VistaPartida> Abandonar(string jugador) =>
        Servicio.PlayMoveAsync(jugador, Codigo, NuevaPeticion(), -1, Jugada.Propuesta(TipoJugada.Abandonar), Ahora);

    /// <summary>Lleva la partida a la fase de habilidades con un héroe colocado por cada lado.</summary>
    public async Task LlevarAHabilidades()
    {
        await Empezar();
        var primero = EnTurno;
        await Colocar(primero, Estado.Jugadores[primero].Mano[0]);
        var segundo = EnTurno;
        await Colocar(segundo, Estado.Jugadores[segundo].Mano[0]);
        Assert.That(Estado.Fase, Is.EqualTo(FaseValores.Habilidades));
    }
}

/// <summary>
/// Pruebas de reglas SIN servicio ni replay: un estado en memoria y llamadas directas al Core.
/// Sirven para montar escenarios que por el camino real son difíciles de alcanzar (un peleador con
/// poca vida, tres héroes en el campo...). Es justo lo que permite tener las reglas en C# puro.
/// </summary>
public static class Mesa
{
    public static readonly DateTime Hora = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Partida ya iniciada entre A y B, en la fase de colocar.</summary>
    public static EstadoPartida Nueva(string faccionA = "F2", string faccionB = "F2", int semilla = 42)
    {
        var s = Reglas.Crear("ABCDEF", Escenario.A, "r-crear", semilla, Hora);
        Reglas.AsignarAnfitrion(s, Reglas.ResolverFaccion(s, faccionA)!);
        Reglas.Unir(s, Escenario.B, Reglas.ResolverFaccion(s, faccionB)!, Hora);
        return s;
    }

    /// <summary>Deja el estado listo para que el SEGUNDO jugador pase y se resuelva la pelea.</summary>
    public static void ListoParaPelear(EstadoPartida s)
    {
        s.Fase = FaseValores.Habilidades;
        s.Paso = 1;
        s.JugadorEnTurnoId = s.RivalDe(s.JugadorInicialId);
    }

    public static Resolucion Pasar(EstadoPartida s) => Reglas.PasarTurno(s, s.JugadorEnTurnoId, Hora);
}
