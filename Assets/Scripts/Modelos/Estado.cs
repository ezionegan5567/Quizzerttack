using System;
using System.Collections.Generic;

// ============ LAB09 UGS: MODELOS (sin dependencias de SDK) ============
// Contrato JSON entre cliente (Unity) y módulo Cloud Code (Tienda).
// DTOs con PROPIEDADes: Newtonsoft (Cloud Save / Cloud Code) los serializa bien.
// OJO: se activan con el define UGS_ON (ver Notas/Sistemas).

namespace Lab09
{
    [Serializable]
    public class HeroeDefensa
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int Nivel { get; set; } = 1;
    }

    // Paso 1: documento de defensa del jugador (público por llave "defensa").
    [Serializable]
    public class DefensaPublica
    {
        public string Nombre { get; set; } = "Jugador";
        public int NivelCuenta { get; set; } = 1;
        public int Trofeos { get; set; } = 0;
        public List<HeroeDefensa> Equipo { get; set; } = new List<HeroeDefensa>();
        public string ActualizadoUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    // Llaves de Cloud Save (dos en una misma petición: documento + nivel para el índice).
    public static class Llaves
    {
        public const string Defensa = "defensa";
        public const string Nivel = "nivel";
        public const string Inventario = "inventario"; // protegido (lo escribe el servidor)
    }

    // ---- DTOs de Cloud Code (módulo Tienda) ----
    [Serializable]
    public class BuscarRivalRequest
    {
        public int Nivel { get; set; }
        public int FranjaMaxima { get; set; } = 10;
        public int CandidatosSuficientes { get; set; } = 2;
        public string PlayerId { get; set; } = "";
    }

    [Serializable]
    public class BuscarRivalResultado
    {
        public bool Hubo { get; set; } = false;
        public string RivalPlayerId { get; set; } = "";
        public DefensaPublica Rival { get; set; }
        public int Franja { get; set; } = 0;
        public int Consultas { get; set; } = 0;
        public int LecturasConsumidas { get; set; } = 0;
        public string Mensaje { get; set; } = "";
    }

    [Serializable]
    public class ResolverCombateRequest
    {
        public string PlayerId { get; set; } = "";
        public string Rival { get; set; } = ""; // playerId real o id de npc (tabla)
        public List<string> Equipo { get; set; } = new List<string>(); // ids declarados
    }

    [Serializable]
    public class ResultadoCombate
    {
        public bool Gano { get; set; } = false;
        public string RivalNombre { get; set; } = "";
        public string Fuente { get; set; } = ""; // "pvp" | "npc" | "practica"
        public int Oro { get; set; } = 0;
        public int Trofeos { get; set; } = 0;
        public string Mensaje { get; set; } = "";
    }
}
