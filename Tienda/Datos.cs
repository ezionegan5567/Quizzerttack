using System;
using System.Collections.Generic;

namespace Tienda
{
    // DTOs espejo del cliente (Newtonsoft: nombres de propiedad deben coincidir).
    public class BuscarRivalRequest
    {
        public int Nivel { get; set; }
        public int FranjaMaxima { get; set; } = 10;
        public int CandidatosSuficientes { get; set; } = 2;
        public string PlayerId { get; set; } = "";
    }

    public class DefensaPublica
    {
        public string Nombre { get; set; } = "Jugador";
        public int NivelCuenta { get; set; } = 1;
        public int Trofeos { get; set; } = 0;
        public List<HeroeDefensa> Equipo { get; set; } = new List<HeroeDefensa>();
        public string ActualizadoUtc { get; set; } = "";
    }

    public class HeroeDefensa
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int Nivel { get; set; } = 1;
    }

    public class BuscarRivalResultado
    {
        public bool Hubo { get; set; }
        public string RivalPlayerId { get; set; } = "";
        public DefensaPublica Rival { get; set; }
        public int Franja { get; set; }
        public int Consultas { get; set; }
        public int LecturasConsumidas { get; set; }
        public string Mensaje { get; set; } = "";
    }

    public class ResolverCombateRequest
    {
        public string PlayerId { get; set; } = "";
        public string Rival { get; set; } = "";
        public List<string> Equipo { get; set; } = new List<string>();
        // LAB 10: semilla para la simulacion determinista e idOperacion para la idempotencia.
        public int Semilla { get; set; } = 0;
        public string IdOperacion { get; set; } = "";
    }

    public class ResultadoCombate
    {
        public bool Gano { get; set; }
        public string RivalNombre { get; set; } = "";
        public string Fuente { get; set; } = "";
        public int Oro { get; set; }
        public int Trofeos { get; set; }
        public string Mensaje { get; set; } = "";
        // LAB 10: progresion devuelta al cliente.
        public int Xp { get; set; }
        public int Nivel { get; set; }
        public List<string> Subieron { get; set; } = new List<string>();
        public List<string> Botin { get; set; } = new List<string>();
        public bool Repetida { get; set; }

        // ---- LAB 08: la pelea verificada contra un rival npc (pasos 5-7) ----
        // Victoria, TurnosJugados, Semilla, Rival, EquipoPropio, EquipoRival, BattleLog,
        // Recompensa, TrofeosCambio y SaldoResultante son lo que el cliente necesita
        // para dibujar la pelea con los datos del servidor, sin recalcular nada.
        public bool Victoria { get { return Gano; } }
        public int TurnosJugados { get; set; }
        public int Semilla { get; set; }
        public string Rival { get; set; } = "";
        public List<UnidadCombate> EquipoPropio { get; set; } = new List<UnidadCombate>();
        public List<UnidadCombate> EquipoRival { get; set; } = new List<UnidadCombate>();
        public List<EntradaBatallaLog> BattleLog { get; set; } = new List<EntradaBatallaLog>();
        public Recompensa Recompensa { get; set; } = new Recompensa();
        public int TrofeosCambio { get; set; }
        public int SaldoResultante { get; set; }
        public bool Repeticion { get; set; }   // RepetirCombate: mismo log, sin volver a premiar
        public bool Rechazado { get; set; }    // el servidor dijo NO y no escribio nada
    }

    // Lo que paga el rival de la tabla (moneda y trofeos), tal cual, sin regateo.
    public class Recompensa
    {
        public string Moneda { get; set; } = "ORO";
        public int Cantidad { get; set; }
    }

    public static class Llaves
    {
        public const string Defensa = "defensa";
        public const string Nivel = "nivel";
        public const string Inventario = "inventario";
        public const string HeroeColoso = "heroe_coloso"; // protegido: no peleable
    }
}
