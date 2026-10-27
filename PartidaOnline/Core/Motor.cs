namespace QuizzerMatch;

/// <summary>
/// El motor: aplica una jugada al estado y reproduce un registro entero. C# puro, sin red ni reloj.
/// Es el equivalente de MatchStateStore.Replay de TTTXO: el estado no se guarda, se DEDUCE de la
/// lista de jugadas con un motor nuevo, de forma determinista (misma semilla + mismas jugadas con las
/// mismas horas = misma partida).
/// </summary>
public static class Motor
{
    /// <summary>
    /// Aplica UNA jugada. El servicio la usa con jugadas nuevas y Reproducir con las guardadas, así
    /// que hay un único camino para validar y aplicar. Las reglas rechazan ANTES de tocar el estado,
    /// de modo que una jugada rechazada nunca deja el estado a medias.
    /// </summary>
    public static Resolucion Aplicar(EstadoPartida s, Jugada j)
    {
        var ahora = Reglas.Parse(j.HoraUtc);
        switch (j.Tipo)
        {
            case TipoJugada.Unirse: return Reglas.Unirse(s, j.JugadorId, j.Faccion, ahora);
            case TipoJugada.Colocar: return Reglas.Colocar(s, j.JugadorId, j.HeroeId, j.Casilla, ahora);
            case TipoJugada.ElegirHabilidad: return Reglas.ElegirHabilidad(s, j.JugadorId, j.PeleadorId, j.IndiceHabilidad, ahora);
            case TipoJugada.ResponderQuiz: return Reglas.ResponderQuiz(s, j.JugadorId, j.PreguntaId, j.Opcion, ahora);
            case TipoJugada.Pasar: return Reglas.PasarTurno(s, j.JugadorId, ahora);
            case TipoJugada.Abandonar: return Reglas.Abandonar(s, j.JugadorId, ahora);
            case TipoJugada.Plazo:
                return Reglas.AplicarPlazo(s, ahora)
                    ? Resolucion.Ok("Plazo vencido.")
                    : Resolucion.Rechazo(Resultado.Invalido, "No había ningún plazo vencido.");
            default:
                return Resolucion.Rechazo(Resultado.Invalido, $"Tipo de jugada desconocido: '{j.Tipo}'.");
        }
    }

    /// <summary>
    /// Reconstruye el estado desde cero. Una jugada guardada que ya no se pueda aplicar significa
    /// que el registro está corrupto o que las reglas cambiaron sin subir GameProtocol.Version:
    /// se lanza una excepción en vez de seguir con un estado inventado.
    /// </summary>
    public static EstadoPartida Reproducir(RegistroPartida r)
    {
        var s = Reglas.Crear(r.Codigo, r.AnfitrionId, r.CreadaPorRequestId, r.Semilla, Reglas.Parse(r.CreadaUtc));
        var faccion = Reglas.ResolverFaccion(s, r.FaccionAnfitrion)
            ?? throw new InvalidOperationException($"Registro corrupto: facción '{r.FaccionAnfitrion}' desconocida.");
        Reglas.AsignarAnfitrion(s, faccion);

        for (var i = 0; i < r.Jugadas.Count; i++)
        {
            var resolucion = Aplicar(s, r.Jugadas[i]);
            if (!resolucion.Aplicada)
                throw new InvalidOperationException(
                    $"No se pudo reproducir la jugada {i} ({r.Jugadas[i].Tipo}) de la partida {r.Codigo}: {resolucion.Mensaje}");
        }
        return s;
    }
}
