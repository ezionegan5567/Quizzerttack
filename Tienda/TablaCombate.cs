using System.Collections.Generic;
using Newtonsoft.Json;

// ============ LAB 08: LAS DOS TABLAS DE CONFIGURACIÓN (Remote Config) ============
// tabla_heroes  -> CUÁNTO vale cada héroe (vida, ataque, defensa, velocidad).
// rivales_npc   -> QUIÉN es cada rival npc (su equipo y su pago).
// Ninguna de las dos se escribe desde el cliente: o vienen de Remote Config o se usan
// estos valores por defecto (el módulo sigue funcionando si la RC no está publicada).

namespace Tienda
{
    public class HeroeTabla
    {
        [JsonProperty("vida")] public int Vida { get; set; } = 100;
        [JsonProperty("ataque")] public int Ataque { get; set; } = 20;
        [JsonProperty("defensa")] public int Defensa { get; set; } = 0;
        [JsonProperty("velocidad")] public int Velocidad { get; set; } = 10;
    }

    public class RivalNpc
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("nombre")] public string Nombre { get; set; } = "";
        [JsonProperty("equipo")] public List<string> Equipo { get; set; } = new List<string>();
        [JsonProperty("moneda")] public int Moneda { get; set; }
        [JsonProperty("trofeos")] public int Trofeos { get; set; }
    }

    public class TablaCombate
    {
        public Dictionary<string, HeroeTabla> Heroes { get; set; } = new Dictionary<string, HeroeTabla>();
        public List<RivalNpc> Rivales { get; set; } = new List<RivalNpc>();

        // Espejo exacto de S08-rivales-npc.rc: si la RC no está publicada, la pelea
        // sale igual (misma tabla de héroes, mismos rivales, mismo pago).
        public static TablaCombate PorDefecto()
        {
            var t = new TablaCombate();

            t.Heroes["bufeador"] = new HeroeTabla { Vida = 90, Ataque = 15, Defensa = 5, Velocidad = 8 };
            t.Heroes["asesino"] = new HeroeTabla { Vida = 80, Ataque = 32, Defensa = 3, Velocidad = 14 };
            t.Heroes["lanza_con_escudo"] = new HeroeTabla { Vida = 150, Ataque = 16, Defensa = 12, Velocidad = 5 };
            t.Heroes["enano"] = new HeroeTabla { Vida = 125, Ataque = 24, Defensa = 9, Velocidad = 4 };
            t.Heroes["vampiro"] = new HeroeTabla { Vida = 110, Ataque = 28, Defensa = 6, Velocidad = 12 };
            t.Heroes["mago"] = new HeroeTabla { Vida = 90, Ataque = 20, Defensa = 4, Velocidad = 9 };
            t.Heroes["cazador"] = new HeroeTabla { Vida = 85, Ataque = 30, Defensa = 3, Velocidad = 13 };
            t.Heroes["guerrero_del_hacha"] = new HeroeTabla { Vida = 115, Ataque = 30, Defensa = 7, Velocidad = 8 };
            t.Heroes["lancero"] = new HeroeTabla { Vida = 100, Ataque = 24, Defensa = 6, Velocidad = 9 };
            t.Heroes["nigromante"] = new HeroeTabla { Vida = 90, Ataque = 22, Defensa = 4, Velocidad = 7 };
            t.Heroes["alquimista"] = new HeroeTabla { Vida = 90, Ataque = 18, Defensa = 4, Velocidad = 8 };
            t.Heroes["pistolero"] = new HeroeTabla { Vida = 85, Ataque = 30, Defensa = 3, Velocidad = 13 };
            t.Heroes["escudero"] = new HeroeTabla { Vida = 150, Ataque = 14, Defensa = 11, Velocidad = 5 };
            t.Heroes["guerrero_dos_hachas"] = new HeroeTabla { Vida = 100, Ataque = 28, Defensa = 5, Velocidad = 10 };
            t.Heroes["hombre_lobo"] = new HeroeTabla { Vida = 110, Ataque = 28, Defensa = 5, Velocidad = 13 };
            t.Heroes["curandero"] = new HeroeTabla { Vida = 95, Ataque = 16, Defensa = 5, Velocidad = 7 };
            t.Heroes["arquero"] = new HeroeTabla { Vida = 85, Ataque = 28, Defensa = 3, Velocidad = 12 };
            t.Heroes["muneco_poseido"] = new HeroeTabla { Vida = 100, Ataque = 20, Defensa = 6, Velocidad = 6 };
            t.Heroes["espadachin"] = new HeroeTabla { Vida = 105, Ataque = 26, Defensa = 6, Velocidad = 11 };
            t.Heroes["draconiano"] = new HeroeTabla { Vida = 120, Ataque = 30, Defensa = 9, Velocidad = 7 };

            t.Rivales.Add(new RivalNpc
            {
                Id = "npc_practica_1",
                Nombre = "Rival de practica",
                Equipo = new List<string> { "escudero", "arquero", "curandero" },
                Moneda = 150,
                Trofeos = 5
            });
            t.Rivales.Add(new RivalNpc
            {
                Id = "npc_practica_2",
                Nombre = "Sombra entrenada",
                Equipo = new List<string> { "lancero", "espadachin", "mago" },
                Moneda = 220,
                Trofeos = 10
            });
            t.Rivales.Add(new RivalNpc
            {
                Id = "npc_jefe_1",
                Nombre = "Coloso de arena",
                Equipo = new List<string> { "draconiano", "guerrero_dos_hachas", "nigromante" },
                Moneda = 400,
                Trofeos = 25
            });
            return t;
        }

        public RivalNpc RivalPorId(string id)
        {
            foreach (var r in Rivales)
                if (r.Id == id) return r;
            return null;
        }
    }
}
