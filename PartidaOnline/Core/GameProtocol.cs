namespace QuizzerMatch;

/// <summary>
/// Versión del contrato cliente-servidor. Existe una copia gemela en el cliente
/// (Assets/Scripts/Online/Partida/PartidaDto.cs) y una prueba falla si dejan de coincidir:
/// súbela en las DOS a la vez cuando cambie la forma de una función, de la vista o de la jugada.
///
/// Sube también esta versión cuando cambien las REGLAS o el catálogo. La partida se guarda como
/// lista de jugadas y se reproduce en cada llamada: si las reglas cambian con partidas a medias,
/// el replay daría otro resultado. Una partida guardada con otra versión se rechaza con un mensaje
/// claro en lugar de reproducirse mal.
///
/// El cliente la comprueba al conectar (GetProtocolVersion) y en cada vista recibida.
/// </summary>
public static class GameProtocol
{
    public const int Version = 1;
}

/// <summary>
/// Tipo (MessageType) del push. Es el único: el push es un AVISO de "algo cambió" y el cliente
/// responde pidiendo GetMatchState. Por eso nunca hace falta distinguir qué pasó ni llevar datos.
/// </summary>
public static class TipoPush
{
    public const string MatchUpdated = "match_updated";
}
