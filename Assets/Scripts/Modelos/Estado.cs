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
        // LAB10: las otras 5 llaves PROTEGIDAS que toca ResolverCombateProgresion.
        public const string Perfil = "perfil";               // {nivelCuenta, experiencia}
        public const string Progreso = "progreso";           // {trofeos, desbloqueos}
        public const string Saldos = "saldos";               // {monedas}
        public const string Operaciones = "operaciones";     // {Ids} (idempotencia)
        public const string UltimoCombate = "ultimo_combate";// {semilla, log, botin, ...}
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
        public string Fuente { get; set; } = ""; // "pvp" | "npc" | "practica" | "repetido"
        public int Oro { get; set; } = 0;
        public int Trofeos { get; set; } = 0;
        public string Mensaje { get; set; } = "";
        // LAB10: progresion devuelta por ResolverCombateProgresion.
        public int Xp { get; set; } = 0;
        public int Nivel { get; set; } = 0;
        public List<string> Subieron { get; set; } = new List<string>();
        public List<string> Botin { get; set; } = new List<string>();
        public bool Repetida { get; set; } = false;
    }

    // LAB10: respuesta de DesbloquearNodo (paso 9).
    [Serializable]
    public class ResultadoNodo
    {
        public bool Exito { get; set; } = false;
        public string Mensaje { get; set; } = "";
        public int Oro { get; set; } = 0;
        public int MonedaRestante { get; set; } = 0;
        public List<string> Desbloqueos { get; set; } = new List<string>();
    }

    // ---- Economía + venganzas (T9Resumen) ----
    [Serializable]
    public class ResumenEntrada
    {
        public string Quien { get; set; } = "";      // alias del atacante
        public string QuienId { get; set; } = "";    // playerId para vengar
        public string Cuando { get; set; } = "";
        public string Resultado { get; set; } = ""; // "te venció" | "perdió contra ti"
    }

    [Serializable]
    public class ResumenBolsa
    {
        public int Oro { get; set; } = 0;
        public int Trofeos { get; set; } = 0;
    }

    [Serializable]
    public class ResumenJugador
    {
        public ResumenBolsa Bolsa { get; set; } = new ResumenBolsa();
        public List<ResumenEntrada> Ataques { get; set; } = new List<ResumenEntrada>();
    }

    // ---- Ranking por trofeos (T9Ranking, índice trofeos) ----
    [Serializable]
    public class FilaRanking
    {
        public string Nombre { get; set; } = "";
        public int Trofeos { get; set; } = 0;
        public int Nivel { get; set; } = 0;
        public string PlayerId { get; set; } = "";
    }

    [Serializable]
    public class RankingResultado
    {
        public List<FilaRanking> Filas { get; set; } = new List<FilaRanking>();
    }
}
