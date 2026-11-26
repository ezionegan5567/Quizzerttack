using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace QuizzerMatch;

/// <summary>
/// Puntos de entrada de Cloud Code, con los nombres del patrón TTTXO: CreateMatch, JoinMatch,
/// PlayMove, GetMatchState (y GetProtocolVersion para el handshake), más QuickMatch / CancelQuickMatch /
/// GetActiveMatch para buscar rival y reanudar (patrón de la rama UGS de Hero Duel). Es una capa fina: valida la
/// forma de los parámetros, saca la identidad de context.PlayerId (nunca de un parámetro) y
/// delega en ServicioPartida. Todas las reglas del juego viven en el Core (Core/Reglas.cs).
///
/// Cloud Code no soporta funciones static, y el cliente envía los argumentos por NOMBRE de
/// parámetro: matchId, requestId... deben llamarse igual en PartidaOnlineService.cs.
///
/// PlayMove es la ÚNICA función de juego: las fases (colocar, elegir habilidad, responder el quiz,
/// pasar, abandonar) son el parámetro `tipo`. Los parámetros son primitivos (los que no use el tipo
/// elegido se ignoran) para no depender de cómo se serialicen objetos anidados.
/// </summary>
public class ModuloPartida
{
    private readonly IGameApiClient _api;
    private readonly ILogger<ModuloPartida> _logger;

    public ModuloPartida(IGameApiClient api, ILogger<ModuloPartida> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>Handshake: el cliente compara su GameProtocol.Version con la del servidor antes de jugar.</summary>
    [CloudCodeFunction("GetProtocolVersion")]
    public int GetProtocolVersion() => GameProtocol.Version;

    [CloudCodeFunction("CreateMatch")]
    public Task<VistaPartida> CreateMatch(IExecutionContext context, IPushClient push, string requestId, string faccion) =>
        Servicio(context, push).CreateMatchAsync(Jugador(context), Peticion(requestId), faccion ?? "", DateTime.UtcNow);

    [CloudCodeFunction("JoinMatch")]
    public Task<VistaPartida> JoinMatch(IExecutionContext context, IPushClient push, string matchId, string requestId, string faccion) =>
        Servicio(context, push).JoinMatchAsync(Jugador(context), Codigo(matchId), Peticion(requestId), faccion ?? "", DateTime.UtcNow);

    [CloudCodeFunction("GetMatchState")]
    public Task<VistaPartida> GetMatchState(IExecutionContext context, IPushClient push, string matchId) =>
        Servicio(context, push).GetMatchStateAsync(Jugador(context), Codigo(matchId), DateTime.UtcNow);

    [CloudCodeFunction("PlayMove")]
    public Task<VistaPartida> PlayMove(IExecutionContext context, IPushClient push, string matchId, string requestId,
        int turnoEsperado, string tipo, string heroeId, int casilla, string peleadorId, int indiceHabilidad,
        int preguntaId, int opcion) =>
        Servicio(context, push).PlayMoveAsync(Jugador(context), Codigo(matchId), Peticion(requestId), turnoEsperado,
            Jugada.Propuesta(tipo, heroeId, casilla, peleadorId, indiceHabilidad, preguntaId, opcion), DateTime.UtcNow);

    /// <summary>"Buscar rival": reanuda tu partida en curso, te une a quien espera, o te deja esperando en una sala propia.</summary>
    [CloudCodeFunction("QuickMatch")]
    public Task<VistaPartida> QuickMatch(IExecutionContext context, IPushClient push, string requestId, string faccion) =>
        Servicio(context, push).QuickMatchAsync(Jugador(context), Peticion(requestId), faccion ?? "", DateTime.UtcNow);

    /// <summary>Sale de la cola y cancela tu sala si seguía esperando rival.</summary>
    [CloudCodeFunction("CancelQuickMatch")]
    public Task<VistaPartida> CancelQuickMatch(IExecutionContext context, IPushClient push) =>
        Servicio(context, push).CancelQuickMatchAsync(Jugador(context), DateTime.UtcNow);

    /// <summary>Reconexión: tu partida en curso, o una vista con Codigo vacío si no tienes ninguna.</summary>
    [CloudCodeFunction("GetActiveMatch")]
    public Task<VistaPartida> GetActiveMatch(IExecutionContext context, IPushClient push) =>
        Servicio(context, push).GetActiveMatchAsync(Jugador(context), DateTime.UtcNow);

    private ServicioPartida Servicio(IExecutionContext context, IPushClient push)
    {
        var cloudSave = new RepositorioCloudSave(_api, context);
        return new(cloudSave, new NotificadorPush(push, context, _logger), null, cloudSave);
    }

    private static string Jugador(IExecutionContext context) =>
        string.IsNullOrEmpty(context.PlayerId)
            ? throw new PartidaException("Esta operación necesita una sesión de jugador iniciada.")
            : context.PlayerId;

    private static string Peticion(string requestId) =>
        string.IsNullOrWhiteSpace(requestId) || requestId.Length > 64
            ? throw new PartidaException("requestId es obligatorio (máximo 64 caracteres) y debe repetirse en los reintentos.")
            : requestId;

    private static string Codigo(string matchId) =>
        CodigoPartida.Normalizar(matchId) ?? throw new PartidaException("Código de partida no válido.");
}
