using System.Security.Cryptography;
using System.Text;

namespace QuizzerMatch;

/// <summary>El registro de la partida más el write lock con el que se leyó (ver TurnMatch).</summary>
public record PartidaGuardada(RegistroPartida Registro, string? WriteLock);

/// <summary>Dónde vive la partida. En producción es Cloud Save; en los tests, un diccionario.</summary>
public interface IRepositorioPartida
{
    /// <summary>Devuelve null si no existe ninguna partida con ese código.</summary>
    Task<PartidaGuardada?> CargarAsync(string codigo);

    /// <summary>true si se guardó; false si otra escritura se adelantó (conflicto de write lock).</summary>
    Task<bool> GuardarAsync(RegistroPartida registro, string? writeLock);
}

/// <summary>Cómo se avisa a un jugador. En producción es un push de Cloud Code.</summary>
public interface INotificador
{
    Task NotificarAsync(string jugadorId, string matchId);
}

/// <summary>Para lo excepcional (código inexistente, no participas). Las jugadas rechazadas NO usan esto.</summary>
public class PartidaException : Exception
{
    public PartidaException(string mensaje) : base(mensaje) { }
}

public static class CodigoPartida
{
    // Sin I, O, 0 ni 1: son los que se confunden al dictar un código en voz alta.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Longitud = 6;

    /// <summary>
    /// Derivado del requestId, no aleatorio: si el cliente reintenta "crear partida" cae sobre la
    /// misma partida en vez de crear otra (idempotencia sin tener que recordar nada). Es el papel
    /// del handoffId en el CreateMatch de TTTXO.
    /// </summary>
    public static string DeRequestId(string requestId, int intento)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId}:{intento}"));
        var sb = new StringBuilder(Longitud);
        for (var i = 0; i < Longitud; i++) sb.Append(Alfabeto[hash[i] % Alfabeto.Length]);
        return sb.ToString();
    }

    public static string? Normalizar(string? codigo)
    {
        var c = (codigo ?? "").Trim().ToUpperInvariant();
        return c.Length == Longitud && c.All(Alfabeto.Contains) ? c : null;
    }
}

/// <summary>
/// Orquesta cada petición: cargar → reproducir → idempotencia → plazo → validar → agregar la
/// jugada → guardar con write lock → avisar. No sabe nada de Cloud Code (todo entra por
/// interfaces), así que se prueba entera en memoria.
///
/// El orden importa (es el de TurnMatch/PlayMove): la idempotencia va antes que cualquier
/// validación, o un reintento legítimo se rechazaría con "no es tu turno" (la primera petición
/// sí llegó y el turno ya pasó al rival).
///
/// Las funciones se llaman como en TTTXO: CreateMatch, JoinMatch, PlayMove, GetMatchState.
/// </summary>
public class ServicioPartida
{
    private const int MaxIntentosCodigo = 5;

    private readonly IRepositorioPartida _repositorio;
    private readonly INotificador _notificador;
    private readonly Func<int> _generarSemilla;

    public ServicioPartida(IRepositorioPartida repositorio, INotificador notificador, Func<int>? generarSemilla = null)
    {
        _repositorio = repositorio;
        _notificador = notificador;
        // La semilla sale del servidor y nunca se envía al cliente.
        _generarSemilla = generarSemilla ?? (() => RandomNumberGenerator.GetInt32(int.MaxValue));
    }

    public async Task<VistaPartida> CreateMatchAsync(string jugadorId, string requestId, string faccion, DateTime ahora)
    {
        if (!Reglas.FaccionValida(faccion))
            throw new PartidaException("Facción no válida. Usa F1, F2, F3, F4 o Random.");

        for (var intento = 0; intento < MaxIntentosCodigo; intento++)
        {
            var codigo = CodigoPartida.DeRequestId(requestId, intento);
            var guardada = await _repositorio.CargarAsync(codigo);

            if (guardada == null)
            {
                var registro = new RegistroPartida
                {
                    Codigo = codigo,
                    AnfitrionId = jugadorId,
                    FaccionAnfitrion = faccion,
                    CreadaPorRequestId = requestId,
                    Semilla = _generarSemilla(),
                    CreadaUtc = Reglas.Iso(ahora),
                };
                var estado = Motor.Reproducir(registro);

                if (!await _repositorio.GuardarAsync(registro, null))
                    return Vistas.Construir(estado, jugadorId, Resultado.Conflicto, "Otra escritura se adelantó. Reintenta.", ahora);
                return Vistas.Construir(estado, jugadorId, Resultado.Aplicado, "Partida creada. Pásale el código a tu rival.", ahora);
            }

            if (guardada.Registro.CreadaPorRequestId == requestId && guardada.Registro.AnfitrionId == jugadorId)
                return Vistas.Construir(Motor.Reproducir(guardada.Registro), jugadorId, Resultado.Repetido,
                    "Esta partida ya se había creado con esta misma petición.", ahora);
            // Colisión con la partida de otra persona: probamos el siguiente código.
        }
        throw new PartidaException("No se ha encontrado un código libre. Reintenta con una petición nueva.");
    }

    public async Task<VistaPartida> JoinMatchAsync(string jugadorId, string matchId, string requestId, string faccion, DateTime ahora)
    {
        var (guardada, estado) = await CargarEstado(matchId);

        if (estado.InvitadoId == jugadorId)
            return Vistas.Construir(estado, jugadorId, Resultado.Repetido, "Ya estabas en esta partida.", ahora);
        if (estado.AnfitrionId == jugadorId)
            throw new PartidaException("No puedes unirte a tu propia partida. Pásale el código a otra persona.");
        if (estado.InvitadoId.Length > 0 || estado.Estado != EstadoValores.EsperandoRival)
            throw new PartidaException("Esta partida ya no admite jugadores.");
        if (!Reglas.FaccionValida(faccion))
            throw new PartidaException("Facción no válida. Usa F1, F2, F3, F4 o Random.");

        var jugada = new Jugada { Tipo = TipoJugada.Unirse, Faccion = faccion }.Sellada(jugadorId, requestId, ahora);
        var resolucion = Motor.Aplicar(estado, jugada);
        if (!resolucion.Aplicada) throw new PartidaException(resolucion.Mensaje);
        guardada.Registro.Jugadas.Add(jugada);

        if (!await _repositorio.GuardarAsync(guardada.Registro, guardada.WriteLock))
            return Vistas.Construir(estado, jugadorId, Resultado.Conflicto, "Otra escritura se adelantó. Reintenta.", ahora);

        await AvisarAlRival(estado, jugadorId);
        return Vistas.Construir(estado, jugadorId, Resultado.Aplicado, "Partida iniciada.", ahora);
    }

    /// <summary>
    /// Devuelve la vista del jugador. Aplica el plazo vencido antes de responder (el reloj es el del
    /// servidor): el plazo se registra como una jugada más, para que el replay lo reproduzca.
    /// </summary>
    public async Task<VistaPartida> GetMatchStateAsync(string jugadorId, string matchId, DateTime ahora)
    {
        var (guardada, estado) = await CargarEstado(matchId);
        if (!estado.Participa(jugadorId))
            throw new PartidaException("No participas en esta partida.");

        var plazo = Jugada.DePlazo(ahora);
        if (Motor.Aplicar(estado, plazo).Aplicada)
        {
            guardada.Registro.Jugadas.Add(plazo);
            if (!await _repositorio.GuardarAsync(guardada.Registro, guardada.WriteLock))
            {
                // Otro cliente aplicó el plazo primero: respondemos con lo que ya está guardado.
                var fresca = await CargarEstado(matchId);
                return Vistas.Construir(fresca.Estado, jugadorId, Resultado.Ok, "", ahora);
            }
            await AvisarAlRival(estado, jugadorId);
        }
        return Vistas.Construir(estado, jugadorId, Resultado.Ok, "", ahora);
    }

    /// <summary>
    /// La única entrada para jugar. <paramref name="propuesta"/> trae el tipo y los datos; el jugador, el
    /// requestId y la hora los pone el servidor. Abandonar no exige turno ni versión.
    /// </summary>
    public async Task<VistaPartida> PlayMoveAsync(string jugadorId, string matchId, string requestId, int turnoEsperado,
        Jugada propuesta, DateTime ahora)
    {
        var (guardada, estado) = await CargarEstado(matchId);
        if (!estado.Participa(jugadorId))
            throw new PartidaException("No participas en esta partida.");

        // 1. Idempotencia: un reintento de una jugada ya aplicada no toca nada.
        if (guardada.Registro.Jugadas.Any(x => x.RequestId == requestId))
            return Vistas.Construir(estado, jugadorId, Resultado.Repetido, "Esta jugada ya se había aplicado.", ahora);

        // 2. Sólo los tipos de jugador: "plazo" y "unirse" los escribe el servidor.
        if (!TipoJugada.EnviablesPorElJugador.Contains(propuesta.Tipo))
            return Vistas.Construir(estado, jugadorId, Resultado.Invalido, $"Tipo de jugada no permitido: '{propuesta.Tipo}'.", ahora);
        var jugada = propuesta.Sellada(jugadorId, requestId, ahora);

        // 3. Plazo vencido (reloj del servidor): se anota como jugada propia.
        var plazo = Jugada.DePlazo(ahora);
        var cambioPorPlazo = Motor.Aplicar(estado, plazo).Aplicada;
        if (cambioPorPlazo) guardada.Registro.Jugadas.Add(plazo);

        // 4. Versión: ¿el cliente va con el estado viejo?  5. Validar y aplicar (las reglas devuelven el rechazo, no lanzan).
        var exigeVersion = jugada.Tipo != TipoJugada.Abandonar && turnoEsperado >= 0;
        Resolucion resolucion;
        if (exigeVersion && turnoEsperado != estado.NumeroTurno)
            resolucion = Resolucion.Rechazo(Resultado.Obsoleto, $"Tu copia va atrasada: aquí vamos por el turno {estado.NumeroTurno}.");
        else
        {
            resolucion = Motor.Aplicar(estado, jugada);
            if (resolucion.Aplicada) guardada.Registro.Jugadas.Add(jugada);
        }

        // Una jugada rechazada no se guarda (el registro sólo tiene jugadas válidas).
        if (!resolucion.Aplicada && !cambioPorPlazo)
            return Vistas.Construir(estado, jugadorId, resolucion.Codigo, resolucion.Mensaje, ahora);

        // 6. Guardar con write lock: si otra escritura se coló, no la pisamos.
        if (!await _repositorio.GuardarAsync(guardada.Registro, guardada.WriteLock))
        {
            var fresca = await CargarEstado(matchId);
            return Vistas.Construir(fresca.Estado, jugadorId, Resultado.Conflicto, "Otra jugada se adelantó. Vuelve a intentarlo.", ahora);
        }

        // 7. Avisar al rival (el que juega ya recibe la respuesta).
        await AvisarAlRival(estado, jugadorId);
        return Vistas.Construir(estado, jugadorId, resolucion.Codigo, resolucion.Mensaje, ahora);
    }

    private async Task AvisarAlRival(EstadoPartida estado, string jugadorId)
    {
        var rival = estado.RivalDe(jugadorId);
        if (rival.Length > 0) await _notificador.NotificarAsync(rival, estado.Codigo);
    }

    /// <summary>Carga el registro y reproduce el estado. Rechaza partidas guardadas con otra versión del contrato.</summary>
    private async Task<(PartidaGuardada Guardada, EstadoPartida Estado)> CargarEstado(string codigo)
    {
        var guardada = await _repositorio.CargarAsync(codigo)
            ?? throw new PartidaException($"No hay ninguna partida con el código {codigo}.");
        if (guardada.Registro.Protocolo != GameProtocol.Version)
            throw new PartidaException(
                $"La partida {codigo} es de una versión anterior del juego (protocolo {guardada.Registro.Protocolo}, el servidor va por {GameProtocol.Version}). Crea una nueva.");
        return (guardada, Motor.Reproducir(guardada.Registro));
    }
}
