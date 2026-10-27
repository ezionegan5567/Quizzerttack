using System;
using System.Collections.Generic;

// ============ PARTIDA ONLINE POR TURNOS: CONTRATO CON EL MÓDULO "QuizzerMatch" ============
// Espejo en cliente de PartidaOnline/Core/Vista.cs. Transporta datos y NADA MÁS: ni una sola
// decisión de juego se toma aquí. Si cambias un campo en el módulo, cámbialo también aquí:
// es un contrato entre las dos mitades (mismos nombres, Newtonsoft los empareja por nombre).
// No necesita UGS_ON: son clases simples.
//
// Una prueba del módulo (ContratoTests) COMPILA este archivo y falla si dejan de coincidir los campos
// de las vistas, los valores de las constantes o GameProtocol.Version. Si la prueba se rompe, no la
// "arregles" a ciegas: es el aviso de que cliente y servidor ya no hablan el mismo idioma.

namespace QuizzerAttack.Partida
{
    /// <summary>
    /// Versión del contrato con el módulo. Gemela de GameProtocol.Version del servidor: súbelas las dos a la
    /// vez cuando cambie una función, la vista o las reglas (ver el comentario del servidor).
    /// </summary>
    public static class GameProtocol
    {
        public const int Version = 1;
    }

    [Serializable]
    public class BuffDto
    {
        public string Tipo { get; set; } = "";
        public int Valor { get; set; }
        public int Rondas { get; set; }
    }

    [Serializable]
    public class HabilidadDto
    {
        public int Indice { get; set; }
        public string Nombre { get; set; } = "";
        public string Efecto { get; set; } = "";
        public bool Disponible { get; set; }
        public bool Usada { get; set; }
    }

    [Serializable]
    public class PeleadorDto
    {
        public string HeroeId { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int Casilla { get; set; }
        public int Vida { get; set; }
        public int VidaMax { get; set; }
        public int HabilidadPreparada { get; set; } = -1;
        public List<int> HabilidadesElegibles { get; set; } = new List<int>();
        public List<HabilidadDto> Habilidades { get; set; } = new List<HabilidadDto>();
        public List<BuffDto> Buffs { get; set; } = new List<BuffDto>();
    }

    /// <summary>Texto y opciones; el índice correcto no existe en el cliente.</summary>
    [Serializable]
    public class PreguntaDto
    {
        public int PreguntaId { get; set; }
        public string Texto { get; set; } = "";
        public List<string> Opciones { get; set; } = new List<string>();
        public string PeleadorId { get; set; } = "";
        public int IndiceHabilidad { get; set; }
        public string VenceUtc { get; set; } = "";
    }

    [Serializable]
    public class VistaPartidaDto
    {
        /// <summary>GameProtocol.Version del servidor. Si no coincide con la del cliente, no se pinta.</summary>
        public int Protocolo { get; set; }
        public string Codigo { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Fase { get; set; } = "";
        public int NumeroTurno { get; set; }
        public int Ronda { get; set; }

        public string TuId { get; set; } = "";
        public string RivalId { get; set; } = "";
        public string JugadorEnTurnoId { get; set; } = "";
        /// <summary>Lo decide el servidor. El cliente sólo lo obedece.</summary>
        public bool EsTuTurno { get; set; }
        public string DeadlineUtc { get; set; } = "";

        public string MiFaccion { get; set; } = "";
        public string RivalFaccion { get; set; } = "";
        public List<string> MiMano { get; set; } = new List<string>();
        public List<string> MiManoNombres { get; set; } = new List<string>();
        public int RivalManoCantidad { get; set; }
        public List<PeleadorDto> MiCampo { get; set; } = new List<PeleadorDto>();
        public List<PeleadorDto> RivalCampo { get; set; } = new List<PeleadorDto>();
        public int RivalPreparados { get; set; }

        public PreguntaDto Pregunta { get; set; }
        public List<string> RegistroPelea { get; set; } = new List<string>();

        public string GanadorId { get; set; } = "";
        public string MotivoFin { get; set; } = "";

        /// <summary>Qué pasó con la petición. Ver <see cref="ResultadoPartida"/>.</summary>
        public string Resultado { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public string ServerTimeUtc { get; set; } = "";
    }

    /// <summary>Los valores de VistaPartidaDto.Resultado. Deben coincidir con Resultado del módulo.</summary>
    public static class ResultadoPartida
    {
        public const string Ok = "ok";
        public const string Aplicado = "aplicado";
        public const string Repetido = "repetido";
        public const string Obsoleto = "obsoleto";
        public const string NoEsTuTurno = "no_es_tu_turno";
        public const string FaseIncorrecta = "fase_incorrecta";
        public const string Invalido = "invalido";
        public const string Terminada = "terminada";
        public const string Conflicto = "conflicto";
    }

    public static class EstadoPartidaValores
    {
        public const string EsperandoRival = "esperando_rival";
        public const string EnJuego = "en_juego";
        public const string Terminada = "terminada";
    }

    public static class FasePartidaValores
    {
        public const string Colocar = "colocar";
        public const string Habilidades = "habilidades";
    }

    /// <summary>El MessageType del push del módulo (TipoPush en el servidor). Es el único: el push es un aviso.</summary>
    public static class TipoPush
    {
        public const string MatchUpdated = "match_updated";
    }

    /// <summary>El cuerpo (mínimo) de un push. No lleva estado ni información oculta: tras recibirlo se llama a GetMatchState.</summary>
    [Serializable]
    public class AvisoPushDto
    {
        public string Type { get; set; } = "";
        public string MatchId { get; set; } = "";
    }
}
