using System.Collections.Generic;

namespace Tienda
{
    // S05: tabla de rivales — stats base de héroes, npcs y pagos.
    public static class TablaRivales
    {
        // stats base por héroe (el cliente declara solo ids; las stats reales viven aquí)
        public static readonly Dictionary<string, (int vida, int ataque)> Heroes = new Dictionary<string, (int, int)>
        {
            { "bufeador", (90, 15) }, { "asesino", (80, 32) },
            { "lanza_con_escudo", (150, 16) }, { "enano", (125, 24) },
            { "vampiro", (110, 28) }, { "mago", (90, 20) },
            { "cazador", (85, 30) }, { "guerrero_del_hacha", (115, 30) },
            { "lancero", (100, 24) }, { "nigromante", (90, 22) },
            { "alquimista", (90, 18) }, { "pistolero", (85, 30) },
            { "escudero", (150, 14) }, { "guerrero_dos_hachas", (100, 28) },
            { "hombre_lobo", (110, 28) }, { "curandero", (95, 16) },
            { "arquero", (85, 28) }, { "muneco_poseido", (100, 20) },
            { "espadachin", (105, 26) }, { "draconiano", (120, 30) },
        };

        // pago por vencer (pvp)
        public const int OroPvp = 100;
        public const int TrofeosPvp = 10;

        // npcs de la tabla (rivales_npc): id, nombre, vida, ataque, oro, trofeos
        public static readonly List<Npc> Npcs = new List<Npc>
        {
            new Npc { Id = "npc_practica_1", Nombre = "Rival de práctica", Vida = 80, Ataque = 12, Oro = 50, Trofeos = 2 },
            new Npc { Id = "npc_practica_2", Nombre = "Sombra entrenada", Vida = 100, Ataque = 16, Oro = 80, Trofeos = 5 },
            new Npc { Id = "npc_jefe_1", Nombre = "Coloso de arena", Vida = 200, Ataque = 25, Oro = 200, Trofeos = 10 },
        };

        public class Npc
        {
            public string Id;
            public string Nombre;
            public int Vida;
            public int Ataque;
            public int Oro;
            public int Trofeos;
        }

        public static Npc NpcPorId(string id)
        {
            foreach (Npc n in Npcs)
                if (n.Id == id) return n;
            return null;
        }

        // poder simple para resolver la batalla en el servidor
        public static int Poder(int integrantes, int ataquePromedio)
        {
            return integrantes * 100 + ataquePromedio * 10;
        }
    }
}
