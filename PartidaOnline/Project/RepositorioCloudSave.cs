using System.Net;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace QuizzerMatch;

/// <summary>
/// El registro de la partida (cabecera + lista de jugadas, solo de agregar) en Cloud Save como custom data PRIVADO, con clave = código de la partida (mismo
/// patrón que TurnMatch del curso). Custom data porque cuelga de la partida y no de un jugador,
/// que es lo que permite que dos personas compartan el mismo estado. Privado porque así sólo
/// Cloud Code (con context.ServiceToken) puede leerlo y escribirlo: ningún cliente puede
/// reescribir la partida. Con el token del jugador esto devolvería Unauthorized, y eso es
/// exactamente lo que debe pasar.
///
/// ACCESS CLASS Private (custom data): el jugador ni lo lee ni lo escribe. Contiene la semilla y las
/// jugadas del rival, que el cliente no debe ver; lo que ve sale de la VistaPartida.
///
/// También guarda lo de "Buscar rival" (IAlmacenSalas), con la misma clase de acceso:
///   - la cola de emparejamiento: entidad "__cola_emparejamiento__", clave "cola";
///   - la partida en curso de cada jugador: entidad "__partidas_activas__", clave = playerId.
/// Ninguna de las dos la puede escribir el jugador: si pudiera, se colaría en la cola o se "uniría" a partidas ajenas.
/// </summary>
public class RepositorioCloudSave : IRepositorioPartida, IAlmacenSalas
{
    private const string Clave = "registro";
    private const string EntidadCola = "__cola_emparejamiento__";
    private const string ClaveCola = "cola";
    private const string EntidadActivas = "__partidas_activas__";

    private readonly IGameApiClient _api;
    private readonly IExecutionContext _context;

    public RepositorioCloudSave(IGameApiClient api, IExecutionContext context)
    {
        _api = api;
        _context = context;
    }

    // ---------------------------------------------------------------- partidas

    public async Task<PartidaGuardada?> CargarAsync(string codigo)
    {
        var doc = await LeerAsync<RegistroPartida>(codigo, Clave);
        return doc == null ? null : new PartidaGuardada(doc.Valor, doc.WriteLock);
    }

    // writeLock nulo = escritura incondicional; es lo correcto al crear la partida.
    public Task<bool> GuardarAsync(RegistroPartida registro, string? writeLock) =>
        EscribirAsync(registro.Codigo, Clave, registro, writeLock);

    // ---------------------------------------------------------------- buscar rival

    public Task<Documento<ColaEmparejamiento>?> CargarColaAsync() => LeerAsync<ColaEmparejamiento>(EntidadCola, ClaveCola);

    // La primera vez la cola no existe y se crea sin write lock: dos búsquedas a la vez en ESE instante podrían
    // pisarse una vez. Es un caso límite aceptado (el peor resultado es que una de las dos tenga que volver a buscar).
    public Task<bool> GuardarColaAsync(ColaEmparejamiento cola, string? writeLock) =>
        EscribirAsync(EntidadCola, ClaveCola, cola, writeLock);

    public async Task<string> PartidaActivaDeAsync(string jugadorId)
    {
        var doc = await LeerAsync<PunteroPartida>(EntidadActivas, jugadorId);
        return doc?.Valor.MatchId ?? "";
    }

    public async Task GuardarPartidaActivaAsync(string jugadorId, string matchId) =>
        await EscribirAsync(EntidadActivas, jugadorId, new PunteroPartida { MatchId = matchId }, null);

    private class PunteroPartida
    {
        public string MatchId { get; set; } = "";
    }

    // ---------------------------------------------------------------- acceso común a custom data privado

    private async Task<Documento<T>?> LeerAsync<T>(string entidad, string clave) where T : class
    {
        try
        {
            var respuesta = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                _context, _context.ServiceToken, _context.ProjectId, entidad,
                new List<string> { clave }, null!, default);

            var item = respuesta.Data?.Results?.FirstOrDefault(i => i.Key == clave);
            if (item?.Value == null) return null;

            var valor = item.Value switch
            {
                JObject json => json.ToObject<T>(),
                string texto when texto.Length > 0 => JObject.Parse(texto).ToObject<T>(),
                var otro => JObject.FromObject(otro).ToObject<T>(),
            };
            return valor == null ? null : new Documento<T>(valor, item.WriteLock);
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;   // todavía no ha escrito nadie ahí: no es un fallo
        }
    }

    private async Task<bool> EscribirAsync(string entidad, string clave, object valor, string? writeLock)
    {
        var cuerpo = new SetItemBody(clave, JObject.FromObject(valor), writeLock!);
        try
        {
            await _api.CloudSaveData.SetPrivateCustomItemAsync(
                _context, _context.ServiceToken, _context.ProjectId, entidad, cuerpo, default);
            return true;
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }
}
