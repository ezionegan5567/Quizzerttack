using System;
using System.Collections.Generic;
using Newtonsoft.Json;

// ============ LAB 10: PROGRESIÓN POST-COMBATE (reglas puras + configuración) ============
// Todo lo de aquí son REGLAS PURAS: no tocan Cloud Save ni la red, así que se pueden
// probar en local (dotnet test) sin desplegar. La integración con el combate (escribir
// las 6 llaves protegidas, idempotencia con idOperación, DesbloquearNodo como
// CloudCodeFunction) vive en Combate/Funciones y en el cliente.
//
// Si la llave "progresion" NO está publicada en Remote Config, el módulo juega con los
// valores por defecto de estas clases (que coinciden con los de la guía).

namespace Tienda
{
    // ---------- Configuración (llave única JSON "progresion") ----------
    public class ConfigProgresion
    {
        [JsonProperty("experiencia")] public ConfigExperiencia Experiencia { get; set; } = new ConfigExperiencia();
        [JsonProperty("niveles")] public ConfigNiveles Niveles { get; set; } = new ConfigNiveles();
        [JsonProperty("botin")] public ConfigBotin Botin { get; set; } = new ConfigBotin();
        // Defaults que replican S10-progresion.rc: si Remote Config no esta publicado,
        // el modulo juega con estos nodos (tallo: 2 de ataque en cadena, 1 de defensa, maestria final).
        [JsonProperty("nodos")] public List<ConfigNodo> Nodos { get; set; } = new List<ConfigNodo>
        {
            new ConfigNodo { Id = "TALENTO_ATAQUE_1",  Requiere = new List<string>(),                                          NivelCuentaMin = 1, CuestaMoneda = 100 },
            new ConfigNodo { Id = "TALENTO_ATAQUE_2",  Requiere = new List<string>{ "TALENTO_ATAQUE_1" },                        NivelCuentaMin = 2, CuestaMoneda = 300 },
            new ConfigNodo { Id = "TALENTO_DEFENSA_1", Requiere = new List<string>(),                                          NivelCuentaMin = 1, CuestaMoneda = 150 },
            new ConfigNodo { Id = "MAESTRIA",          Requiere = new List<string>{ "TALENTO_ATAQUE_2", "TALENTO_DEFENSA_1" },   NivelCuentaMin = 3, CuestaMoneda = 500 },
        };
    }

    // victoria 50, derrota 15, +1 por turno con tope de 20, 5 por héroe caído.
    public class ConfigExperiencia
    {
        [JsonProperty("victoria")] public int Victoria { get; set; } = 50;
        [JsonProperty("derrota")] public int Derrota { get; set; } = 15;
        [JsonProperty("porTurno")] public int PorTurno { get; set; } = 1;
        [JsonProperty("topeTurnos")] public int TopeTurnos { get; set; } = 20;
        [JsonProperty("porCaido")] public int PorCaido { get; set; } = 5;
        // decisión de diseño: los héroes que TERMINAN caídos no ganan XP
        [JsonProperty("caidosGanan")] public bool CaidosGanan { get; set; } = false;
    }

    // máximo 10; subir del nivel n al n+1 cuesta costoBase · n²  (100, 400, 900 ... 8·100=... )
    public class ConfigNiveles
    {
        [JsonProperty("maximo")] public int Maximo { get; set; } = 10;
        [JsonProperty("costoBase")] public int CostoBase { get; set; } = 100;
    }

    // tabla de botín: tiradas + premios (id, tipo, cantidad, peso). cantidad 0 = «no cae nada».
    public class ConfigBotin
    {
        [JsonProperty("tiradas")] public int Tiradas { get; set; } = 1;
        // Default replica el .rc: espada(40%) / pocion x2(40%) / nada(20%).
        [JsonProperty("premios")] public List<PremioBotin> Premios { get; set; } = new List<PremioBotin>
        {
            new PremioBotin { Id = "espada", Tipo = "objeto", Cantidad = 1, Peso = 40 },
            new PremioBotin { Id = "pocion", Tipo = "objeto", Cantidad = 2, Peso = 40 },
            new PremioBotin { Id = "nada",   Tipo = "objeto", Cantidad = 0, Peso = 20 },
        };
    }

    public class PremioBotin
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("tipo")] public string Tipo { get; set; } = "objeto"; // "objeto" -> inventario | "moneda" -> saldos
        [JsonProperty("cantidad")] public int Cantidad { get; set; } = 0;
        [JsonProperty("peso")] public int Peso { get; set; } = 0;
    }

    // nodo del tallo del jugador: prerrequisitos, nivel de cuenta mínimo y coste en moneda.
    public class ConfigNodo
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("requiere")] public List<string> Requiere { get; set; } = new List<string>();
        [JsonProperty("nivelCuentaMin")] public int NivelCuentaMin { get; set; } = 1;
        [JsonProperty("cuestaMoneda")] public int CuestaMoneda { get; set; } = 0;
    }

    // Resultado de intentar desbloquear un nodo (paso 9). Mensaje = motivo si no se puede.
    public class ResultadoNodo
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = "";
        public int Oro { get; set; }
        public int MonedaRestante { get; set; }
        public List<string> Desbloqueos { get; set; } = new List<string>();
    }

    // ---------- REGLAS PURAS ----------
    public static class ReglasProgresion
    {
        // PASO 3: experiencia ganada por el superviviente (fórmula leída de configuración).
        // base de victoria/derrota + porTurno · min(turnos, topeTurnos).
        // Verificado: victoria en 11 turnos -> 61 XP; 12 -> 62.
        public static int ExperienciaDeCombate(ConfigExperiencia cfg, bool victoria, int turnos)
        {
            if (cfg == null) cfg = new ConfigExperiencia();
            int baseXp = victoria ? cfg.Victoria : cfg.Derrota;
            int porTurnos = cfg.PorTurno * Math.Min(turnos, cfg.TopeTurnos);
            return baseXp + porTurnos;
        }

        // Coste de pasar del nivel `nivel` al siguiente: costoBase · nivel².
        public static int CostoParaSubir(ConfigNiveles niveles, int nivel)
        {
            if (niveles == null) niveles = new ConfigNiveles();
            return niveles.CostoBase * nivel * nivel;
        }

        // PASO 4: subida de nivel EN BUCLE, conservando el sobrante (varios niveles en un combate).
        // Verificado: 90 xp + 600 -> nivel 3, 190 de sobrante, 2 subidas (costoBase 100).
        public static (int nivel, int experiencia, int subidos) AplicarNiveles(
            ConfigNiveles niveles, int nivel, int experiencia, int ganada)
        {
            if (niveles == null) niveles = new ConfigNiveles();
            var subidos = 0;
            if (nivel >= niveles.Maximo) return (niveles.Maximo, 0, 0);

            var total = experiencia + Math.Max(0, ganada);
            while (nivel < niveles.Maximo &&
                   total >= CostoParaSubir(niveles, nivel))
            {
                total -= CostoParaSubir(niveles, nivel); // costoBase · n²
                nivel++;
                subidos++;
            }
            return nivel >= niveles.Maximo
                ? (niveles.Maximo, 0, subidos)
                : (nivel, total, subidos);
        }

        // PASO 5: botín en el servidor con semilla SEMBRADA.
        // Usa una corriente de azar DISTINTA a la de la simulación (semilla ^ 0x5EED) para no
        // alterar el battle log. Solo se llama al ganar. Verificado: 4000 semillas -> ~40/40/20.
        public static List<PremioBotin> TirarBotin(ConfigBotin botin, int semilla)
        {
            var obtenidos = new List<PremioBotin>();
            if (botin == null || botin.Premios == null || botin.Premios.Count == 0) return obtenidos;

            int pesoTotal = 0;
            foreach (var p in botin.Premios) pesoTotal += Math.Max(0, p.Peso);
            if (pesoTotal <= 0) return obtenidos;

            // Otra corriente que la simulación: el battle log no cambia.
            var azar = new Random(semilla ^ 0x5EED);
            int tiradas = botin.Tiradas <= 0 ? 1 : botin.Tiradas;
            for (var tirada = 0; tirada < tiradas; tirada++)
            {
                var punto = azar.Next(pesoTotal);
                foreach (var premio in botin.Premios)
                {
                    punto -= premio.Peso;
                    if (punto < 0) { obtenidos.Add(premio); break; }
                }
            }
            return obtenidos; // «NADA»: cantidad 0, no cae nada
        }

        // PASO 9 (núcleo): por qué NO se puede desbloquear un nodo. Devuelve null si se puede.
        // Orden verificado en la guía: prerrequisitos -> ya desbloqueado -> nivel de cuenta -> precio.
        public static string PorQueNoSePuede(
            ConfigNodo nodo, List<string> desbloqueos, int nivelCuenta, int saldoMoneda)
        {
            if (nodo == null) return "El nodo no existe.";
            var ya = desbloqueos ?? new List<string>();

            // 1) prerrequisitos
            var faltantes = new List<string>();
            foreach (var r in nodo.Requiere ?? new List<string>())
                if (!ya.Contains(r)) faltantes.Add(r);
            if (faltantes.Count > 0)
                return "Falta desbloquear antes: " + string.Join(", ", faltantes);

            // 2) ya desbloqueado (sin segundo cobro)
            if (ya.Contains(nodo.Id))
                return "ya está desbloqueado";

            // 3) nivel de cuenta mínimo
            if (nivelCuenta < nodo.NivelCuentaMin)
                return "Requiere nivel de cuenta " + nodo.NivelCuentaMin;

            // 4) precio en moneda
            if (saldoMoneda < nodo.CuestaMoneda)
                return "Falta oro: necesitas " + nodo.CuestaMoneda;

            return null; // procede
        }
    }
}
