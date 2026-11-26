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

/// <summary>Un documento leído con su write lock (para escribirlo después sin pisar a nadie).</summary>
public record Documento<T>(T Valor, string? WriteLock);

/// <summary>
/// Lo que necesita "Buscar rival" además de las partidas: la cola de emparejamiento y, por jugador, qué partida
/// tiene en curso (para reanudarla y para que nadie esté en dos partidas a la vez). En producción es Cloud Save.
/// </summary>
public interface IAlmacenSalas
{
    /// <summary>null si la cola todavía no existe.</summary>
    Task<Documento<ColaEmparejamiento>?> CargarColaAsync();

    /// <summary>true si se guardó; false si otra búsqueda escribió antes (hay que recargar y reintentar).</summary>
    Task<bool> GuardarColaAsync(ColaEmparejamiento cola, string? writeLock);

    /// <summary>El código de la partida en curso del jugador, o "" si no tiene.</summary>
    Task<string> PartidaActivaDeAsync(string jugadorId);

    /// <summary>Apunta (o borra, con "") la partida en curso del jugador.</summary>
    Task GuardarPartidaActivaAsync(string jugadorId, string matchId);
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

    private const int MaxIntentosCola = 5;
    private const int MaxRivalesAProbar = 3;

    private readonly IRepositorioPartida _repositorio;
    private readonly INotificador _notificador;
    private readonly Func<int> _generarSemilla;
    private readonly IAlmacenSalas? _salas;

    /// <param name="salas">Cola de "Buscar rival" y partida activa por jugador. Sin él (pruebas antiguas) no hay
    /// emparejamiento automático ni la regla de "una partida a la vez".</param>
    public ServicioPartida(IRepositorioPartida repositorio, INotificador notificador, Func<int>? generarSemilla = null,
        IAlmacenSalas? salas = null)
    {
        _repositorio = repositorio;
        _notificador = notificador;
        // La semilla sale del servidor y nunca se envía al cliente.
        _generarSemilla = generarSemilla ?? (() => RandomNumberGenerator.GetInt32(int.MaxValue));
        _salas = salas;
    }

    public async Task<VistaPartida> CreateMatchAsync(string jugadorId, string requestId, string faccion, DateTime ahora,
        bool porBusqueda = false)
    {
        if (!Reglas.FaccionValida(faccion))
            throw new PartidaException("Facción no válida. Usa F1, F2, F3, F4 o Random.");

        // Una partida a la vez (salvo que sea el reintento de la misma creación).
        var activa = await PartidaActivaVigente(jugadorId);
        if (activa != null && activa.CreadaPorRequestId != requestId)
            throw new PartidaException($"Ya tienes una partida en curso (código {activa.Codigo}). Reanúdala o abandónala antes de empezar otra.");

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
                    PorBusqueda = porBusqueda,
                    Semilla = _generarSemilla(),
                    CreadaUtc = Reglas.Iso(ahora),
                };
                var estado = Motor.Reproducir(registro);

                if (!await _repositorio.GuardarAsync(registro, null))
                    return Vistas.Construir(estado, jugadorId, Resultado.Conflicto, "Otra escritura se adelantó. Reintenta.", ahora);
                await FijarActiva(jugadorId, codigo);
                return Vistas.Construir(estado, jugadorId, Resultado.Aplicado, "Partida creada. Pásale el código a tu rival.", ahora);
            }

            // Reintento de la misma creación: la misma partida. Salvo que ya terminara (por ejemplo, una sala de
            // "Buscar rival" cancelada): entonces se crea otra con el siguiente código en vez de devolver una partida muerta.
            if (guardada.Registro.CreadaPorRequestId == requestId && guardada.Registro.AnfitrionId == jugadorId
                && guardada.Registro.Protocolo == GameProtocol.Version
                && Motor.Reproducir(guardada.Registro).Estado != EstadoValores.Terminada)
            {
                await FijarActiva(jugadorId, codigo);
                return Vistas.Construir(Motor.Reproducir(guardada.Registro), jugadorId, Resultado.Repetido,
                    "Esta partida ya se había creado con esta misma petición.", ahora);
            }
            // Colisión con la partida de otra persona: probamos el siguiente código.
        }
        throw new PartidaException("No se ha encontrado un código libre. Reintenta con una petición nueva.");
    }

    public async Task<VistaPartida> JoinMatchAsync(string jugadorId, string matchId, string requestId, string faccion, DateTime ahora)
    {
        var (guardada, estado) = await CargarEstado(matchId);

        if (estado.InvitadoId == jugadorId)
            return Vistas.Construir(estado, jugadorId, Resultado.Repetido, "Ya estabas en esta partida.", ahora);
        var activa = await PartidaActivaVigente(jugadorId);
        if (activa != null && activa.Codigo != matchId)
            throw new PartidaException($"Ya tienes una partida en curso (código {activa.Codigo}). Reanúdala o abandónala antes de unirte a otra.");
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

        await FijarActiva(jugadorId, matchId);
        await AvisarAlRival(estado, jugadorId);
        return Vistas.Construir(estado, jugadorId, Resultado.Aplicado, "Partida iniciada.", ahora);
    }

    // ---------------------------------------------------------------- buscar rival (patrón de Hero Duel, rama UGS)

    /// <summary>
    /// "Buscar rival": si ya tienes una partida en curso, te la devuelve (reanudar). Si no, reclama al jugador que
    /// más tiempo lleva esperando y se une a su sala. Si no hay nadie, crea una sala propia y te anota en la cola.
    /// La cola se escribe con write lock: dos búsquedas simultáneas nunca reclaman al mismo rival.
    /// </summary>
    public async Task<VistaPartida> QuickMatchAsync(string jugadorId, string requestId, string faccion, DateTime ahora)
    {
        var salas = _salas ?? throw new InvalidOperationException("Buscar rival necesita un IAlmacenSalas.");
        if (!Reglas.FaccionValida(faccion))
            throw new PartidaException("Facción no válida. Usa F1, F2, F3, F4 o Random.");

        // 1. ¿Ya estás en una partida? Se reanuda en vez de abrir otra (un reintento cae aquí también).
        var activa = await PartidaActivaVigente(jugadorId);
        if (activa != null && activa.Estado == EstadoValores.EnJuego)
        {
            var vista = await GetMatchStateAsync(jugadorId, activa.Codigo, ahora);
            vista.Resultado = Resultado.Repetido;
            vista.Mensaje = "Ya tienes una partida en curso: reanudada.";
            return vista;
        }
        if (activa != null)
        {
            // Tu sala sigue vacía. Si hay alguien más esperando, mejor jugar con él que seguir esperando los dos en
            // salas distintas: se cancela la tuya y se sigue al paso 2. Si no hay nadie, sigues en tu sala.
            var hayOtro = await ModificarCola(c => { Emparejamiento.Podar(c, ahora); return Emparejamiento.Candidato(c, jugadorId) != null; });
            if (!hayOtro || activa.AnfitrionId != jugadorId)
            {
                // Esperar aquí lo pone en la cola (aunque su sala fuera de código: ahora está buscando rival).
                await ModificarCola(c => { Emparejamiento.Podar(c, ahora); Emparejamiento.Anotar(c, jugadorId, activa.Codigo, ahora); return true; });
                var vista = await GetMatchStateAsync(jugadorId, activa.Codigo, ahora);
                vista.Resultado = Resultado.Repetido;
                vista.Mensaje = "Sigues buscando rival en tu sala " + activa.Codigo + ".";
                return vista;
            }
            await CancelQuickMatchAsync(jugadorId, ahora);
        }

        // 2. Reclamar a un rival que espera. Si su sala ya no admite (la canceló, o se la llevó otro), se prueba el siguiente.
        for (var intento = 0; intento < MaxRivalesAProbar; intento++)
        {
            var reclamado = await ModificarCola(c =>
            {
                Emparejamiento.Podar(c, ahora);
                var candidato = Emparejamiento.Candidato(c, jugadorId);
                if (candidato != null) c.Entradas.Remove(candidato);
                return candidato;
            });
            if (reclamado == null) break;

            VistaPartida unida;
            try { unida = await JoinMatchAsync(jugadorId, reclamado.MatchId, requestId, faccion, ahora); }
            catch (PartidaException) { continue; }   // sala inexistente, cancelada o ya llena: se descarta su entrada

            if (unida.Resultado == Resultado.Conflicto)
            {
                // La sala sigue esperando: devolver al rival a la cola para no dejarlo colgado.
                await ModificarCola(c => { if (c.Entradas.All(e => e.JugadorId != reclamado.JugadorId)) c.Entradas.Add(reclamado); return true; });
                continue;
            }
            unida.Mensaje = "¡Rival encontrado! Empieza la partida.";
            return unida;
        }

        // 3. Nadie esperando: sala propia y a la cola.
        var creada = await CreateMatchAsync(jugadorId, requestId, faccion, ahora, porBusqueda: true);
        if (creada.Resultado == Resultado.Conflicto) return creada;
        await ModificarCola(c => { Emparejamiento.Podar(c, ahora); Emparejamiento.Anotar(c, jugadorId, creada.Codigo, ahora); return true; });
        creada.Mensaje = "Buscando rival… Tu sala es " + creada.Codigo + " (también puedes pasarle el código a alguien).";
        return creada;
    }

    /// <summary>Sale de la cola y, si tu sala seguía esperando rival, la cancela. No toca partidas ya empezadas.</summary>
    public async Task<VistaPartida> CancelQuickMatchAsync(string jugadorId, DateTime ahora)
    {
        if (_salas == null) throw new InvalidOperationException("Cancelar la búsqueda necesita un IAlmacenSalas.");
        var estabaEnCola = await ModificarCola(c => { Emparejamiento.Podar(c, ahora); return Emparejamiento.Quitar(c, jugadorId); });

        var activa = await PartidaActivaVigente(jugadorId);
        if (activa != null && activa.Estado == EstadoValores.EsperandoRival && activa.AnfitrionId == jugadorId)
        {
            var vista = await PlayMoveAsync(jugadorId, activa.Codigo, "cancelar:" + Reglas.Iso(ahora), -1,
                Jugada.Propuesta(TipoJugada.Abandonar), ahora);
            await FijarActiva(jugadorId, "");
            vista.Mensaje = "Búsqueda cancelada.";
            return vista;
        }
        return VistaSinPartida(estabaEnCola ? "Búsqueda cancelada." : "No estabas buscando rival.", ahora);
    }

    /// <summary>Reconexión: la partida en curso del jugador (con su vista), o una vista vacía (Codigo = "") si no tiene.</summary>
    public async Task<VistaPartida> GetActiveMatchAsync(string jugadorId, DateTime ahora)
    {
        var activa = await PartidaActivaVigente(jugadorId);
        if (activa == null) return VistaSinPartida("No tienes ninguna partida en curso.", ahora);

        var vista = await GetMatchStateAsync(jugadorId, activa.Codigo, ahora);   // aplica los plazos vencidos
        if (vista.Estado == EstadoValores.Terminada) await FijarActiva(jugadorId, "");
        vista.Mensaje = vista.Estado == EstadoValores.Terminada ? "Tu última partida ya terminó."
            : vista.Estado == EstadoValores.EsperandoRival ? "Tienes una sala esperando rival."
            : "Tienes una partida en curso: reanudada.";
        return vista;
    }

    /// <summary>La partida en curso del jugador según su puntero, o null. Limpia punteros a partidas que ya no valen.</summary>
    private async Task<EstadoPartida?> PartidaActivaVigente(string jugadorId)
    {
        if (_salas == null) return null;
        var codigo = await _salas.PartidaActivaDeAsync(jugadorId);
        if (codigo.Length == 0) return null;

        var guardada = await _repositorio.CargarAsync(codigo);
        EstadoPartida? estado = null;
        if (guardada != null && guardada.Registro.Protocolo == GameProtocol.Version)
            estado = Motor.Reproducir(guardada.Registro);
        if (estado == null || !estado.Participa(jugadorId) || estado.Estado == EstadoValores.Terminada)
        {
            await FijarActiva(jugadorId, "");
            return null;
        }
        return estado;
    }

    /// <summary>El puntero es una ayuda (reanudar, una partida a la vez): si falla, la partida sigue valiendo.</summary>
    private async Task FijarActiva(string jugadorId, string matchId)
    {
        if (_salas == null) return;
        try { await _salas.GuardarPartidaActivaAsync(jugadorId, matchId); }
        catch (Exception) { /* best-effort: lo arregla la próxima llamada */ }
    }

    /// <summary>
    /// Vuelve a anotar (con la hora de ahora) a quien sigue esperando. Para no escribir la cola en cada consulta,
    /// sólo lo hace si su entrada falta o tiene más de un cuarto de la vida máxima.
    /// </summary>
    private async Task RefrescarEnCola(string jugadorId, string matchId, DateTime ahora)
    {
        if (_salas == null) return;
        var doc = await _salas.CargarColaAsync();
        var entrada = doc?.Valor.Entradas.FirstOrDefault(e => e.JugadorId == jugadorId && e.MatchId == matchId);
        if (entrada != null && (ahora - Reglas.Parse(entrada.DesdeUtc)).TotalSeconds < Emparejamiento.SegundosEnCola / 4.0) return;
        try { await ModificarCola(c => { Emparejamiento.Podar(c, ahora); Emparejamiento.Anotar(c, jugadorId, matchId, ahora); return true; }); }
        catch (PartidaException) { /* cola saturada: se reintenta en la próxima consulta */ }
    }

    /// <summary>Carga la cola, aplica el cambio y la guarda con write lock; si otra búsqueda escribió antes, repite.</summary>
    private async Task<T> ModificarCola<T>(Func<ColaEmparejamiento, T> cambio)
    {
        for (var i = 0; i < MaxIntentosCola; i++)
        {
            var doc = await _salas!.CargarColaAsync();
            var cola = doc?.Valor ?? new ColaEmparejamiento();
            var resultado = cambio(cola);
            if (await _salas.GuardarColaAsync(cola, doc?.WriteLock)) return resultado;
        }
        throw new PartidaException("La cola de emparejamiento está muy ocupada. Vuelve a intentarlo en un momento.");
    }

    private static VistaPartida VistaSinPartida(string mensaje, DateTime ahora) => new()
    {
        Protocolo = GameProtocol.Version,
        Resultado = Resultado.Ok,
        Mensaje = mensaje,
        ServerTimeUtc = Reglas.Iso(ahora),
    };

    /// <summary>
    /// Devuelve la vista del jugador. Aplica el plazo vencido antes de responder (el reloj es el del
    /// servidor): el plazo se registra como una jugada más, para que el replay lo reproduzca.
    /// </summary>
    public async Task<VistaPartida> GetMatchStateAsync(string jugadorId, string matchId, DateTime ahora)
    {
        var (guardada, estado) = await CargarEstado(matchId);
        if (!estado.Participa(jugadorId))
            throw new PartidaException("No participas en esta partida.");

        // Quien espera rival con el juego abierto consulta cada ~10 s (sondeo): eso mantiene viva su entrada en la cola.
        // Si cierra el juego deja de consultar y caduca a los Emparejamiento.SegundosEnCola. Sólo salas de "Buscar rival".
        if (guardada.Registro.PorBusqueda && estado.Estado == EstadoValores.EsperandoRival && estado.AnfitrionId == jugadorId)
            await RefrescarEnCola(jugadorId, matchId, ahora);

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
