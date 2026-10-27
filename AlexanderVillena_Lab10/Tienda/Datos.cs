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
    }

    public static class Llaves
    {
        public const string Defensa = "defensa";
        public const string Nivel = "nivel";
        public const string Inventario = "inventario";
        public const string HeroeColoso = "heroe_coloso"; // protegido: no peleable
    }
}
