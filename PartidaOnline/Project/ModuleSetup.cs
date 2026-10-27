using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace QuizzerMatch;

/// <summary>
/// Registra lo que Cloud Code inyecta en el módulo. Hace falta el cliente de las APIs de juego
/// porque la partida vive en Cloud Save. IPushClient se inyecta solo (Core >= 0.0.4) como
/// parámetro de las funciones, no hay que registrarlo aquí.
/// </summary>
public class ModuleSetup : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.AddGameApiClient();
    }
}
