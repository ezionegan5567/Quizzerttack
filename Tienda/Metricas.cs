// ============ LAB14: MÉTRICAS POR OPERACIÓN (registro estructurado) ============
// Una línea de log por operación, con su costo:
//   operacion="ResolverCombate" id="61fa..." resultado="ok" duracionMs=182 lecturas=4 lecturasCloudSave=2 lecturasRemoteConfig=2 escrituras=2
// - id = idOperacion que manda el cliente (un GUID), NO el jugador. BuscarRival no lo
//   recibe: va "-".
// - resultado: "ok" | "rechazada" (la función respondió que no se puede) | "error"
//   (lanzó una excepción).
// - NUNCA se registran tokens, nombres ni correos. El logger de Cloud Code agrega
//   playerId/environmentId/projectId por su cuenta (son opacos y hacen falta para soporte).
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Tienda
{
    public sealed class ContadorOperacion
    {
        readonly Stopwatch _reloj = Stopwatch.StartNew();
        public int LecturasCloudSave { get; private set; }
        public int LecturasRemoteConfig { get; private set; }
        public int Lecturas => LecturasCloudSave + LecturasRemoteConfig;
        public int Escrituras { get; private set; }
        public void LeerCloudSave(int n = 1) => LecturasCloudSave += n;
        public void LeerRemoteConfig(int n = 1) => LecturasRemoteConfig += n;
        public void Escribir(int n = 1) => Escrituras += n;
        public long Milisegundos => _reloj.ElapsedMilliseconds;
    }

    public static class Metricas
    {
        public static ContadorOperacion Iniciar() => new ContadorOperacion();

        // Una sola línea por operación (plantilla con atributos estructurados).
        public static void Registrar(ILogger logger, string operacion, string idOperacion,
                                     string resultado, ContadorOperacion contador)
        {
            if (contador == null) contador = new ContadorOperacion();
            string id = string.IsNullOrEmpty(idOperacion) ? "-" : idOperacion;

            logger?.LogInformation(
                "operacion=\"{Operacion}\" id=\"{IdOperacion}\" resultado=\"{Resultado}\" " +
                "duracionMs={DuracionMs} lecturas={Lecturas} lecturasCloudSave={LecturasCloudSave} " +
                "lecturasRemoteConfig={LecturasRemoteConfig} escrituras={Escrituras}",
                operacion, id, resultado, contador.Milisegundos, contador.Lecturas,
                contador.LecturasCloudSave, contador.LecturasRemoteConfig, contador.Escrituras);
        }
    }
}
