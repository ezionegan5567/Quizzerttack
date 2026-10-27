using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace QuizzerMatch;

/// <summary>
/// Avisa a UN jugador por push de Cloud Code (Wire: WebSocket, servidor -> cliente).
///
/// Reglas que se cumplen aquí a propósito (las del patrón TTTXO):
///  - Sólo SendPlayerMessage con un destinatario calculado por el servidor. Nunca
///    SendProjectMessage (llegaría a todo el proyecto) ni un endpoint genérico que reciba
///    el playerId del cliente: cualquiera podría suplantar un aviso.
///  - El mensaje es un AVISO mínimo {type, matchId}. No lleva estado ni información oculta:
///    el cliente reacciona llamando a GetMatchState, que sí construye su vista privada, y no
///    se fía del contenido del push para decidir nada.
///  - Un push que falla no debe romper la jugada (ya está guardada): se registra y se sigue.
///    Si el jugador no estaba conectado no recibe nada; el cliente tiene un sondeo de respaldo
///    (~10 s) y al volver llama a GetMatchState.
/// </summary>
public class NotificadorPush : INotificador
{
    private readonly IPushClient _push;
    private readonly IExecutionContext _context;
    private readonly ILogger _logger;

    public NotificadorPush(IPushClient push, IExecutionContext context, ILogger logger)
    {
        _push = push;
        _context = context;
        _logger = logger;
    }

    /// <summary>El cuerpo del push: sólo el tipo y la partida. Separado para poder probar que no lleva nada más.</summary>
    public static string CrearMensaje(string matchId) =>
        JsonConvert.SerializeObject(new { type = TipoPush.MatchUpdated, matchId });

    public async Task NotificarAsync(string jugadorId, string matchId)
    {
        try
        {
            await _push.SendPlayerMessageAsync(_context, CrearMensaje(matchId), TipoPush.MatchUpdated, jugadorId);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "No se pudo enviar el push al jugador {Jugador} (partida {Partida})", jugadorId, matchId);
        }
    }
}
