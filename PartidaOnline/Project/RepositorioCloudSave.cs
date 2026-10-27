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
/// </summary>
public class RepositorioCloudSave : IRepositorioPartida
{
    private const string Clave = "registro";

    private readonly IGameApiClient _api;
    private readonly IExecutionContext _context;

    public RepositorioCloudSave(IGameApiClient api, IExecutionContext context)
    {
        _api = api;
        _context = context;
    }

    public async Task<PartidaGuardada?> CargarAsync(string codigo)
    {
        try
        {
            var respuesta = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                _context, _context.ServiceToken, _context.ProjectId, codigo,
                new List<string> { Clave }, null!, default);

            var item = respuesta.Data?.Results?.FirstOrDefault(i => i.Key == Clave);
            if (item?.Value == null) return null;

            var registro = item.Value switch
            {
                JObject json => json.ToObject<RegistroPartida>(),
                string texto when texto.Length > 0 => JObject.Parse(texto).ToObject<RegistroPartida>(),
                var otro => JObject.FromObject(otro).ToObject<RegistroPartida>(),
            };
            return registro == null ? null : new PartidaGuardada(registro, item.WriteLock);
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;   // todavía no ha escrito nadie con ese código: no es un fallo
        }
    }

    public async Task<bool> GuardarAsync(RegistroPartida registro, string? writeLock)
    {
        // writeLock nulo = escritura incondicional; es lo correcto al crear la partida.
        var cuerpo = new SetItemBody(Clave, JObject.FromObject(registro), writeLock!);
        try
        {
            await _api.CloudSaveData.SetPrivateCustomItemAsync(
                _context, _context.ServiceToken, _context.ProjectId, registro.Codigo, cuerpo, default);
            return true;
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }
}
