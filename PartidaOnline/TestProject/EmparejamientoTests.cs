using QuizzerMatch;

namespace QuizzerMatch.Tests;

/// <summary>
/// "Buscar rival" (QuickMatch), cancelar la búsqueda, reanudar y "una partida a la vez".
/// El patrón es el de la rama UGS de Hero Duel: una cola en Cloud Save y un puntero a la partida activa por jugador,
/// con write lock en la cola para que dos búsquedas simultáneas no reclamen al mismo rival.
/// </summary>
[TestFixture]
public class EmparejamientoTests
{
    private const string C = "jugador-C";

    private static Task<VistaPartida> Buscar(Escenario e, string jugador, string faccion = "F1") =>
        e.Servicio.QuickMatchAsync(jugador, e.NuevaPeticion(), faccion, e.Ahora);

    // ---------------------------------------------------------------- emparejar

    [Test]
    public async Task DosJugadoresQueBuscan_QuedanEnLaMismaPartida_YAlPrimeroLeLlegaElAviso()
    {
        var e = new Escenario();

        var a = await Buscar(e, Escenario.A);
        Assert.That(a.Estado, Is.EqualTo(EstadoValores.EsperandoRival));
        Assert.That(a.Mensaje, Does.Contain("Buscando rival"));
        Assert.That(e.Salas.Cola.Entradas.Single().JugadorId, Is.EqualTo(Escenario.A));

        var b = await Buscar(e, Escenario.B, "F2");

        Assert.That(b.Estado, Is.EqualTo(EstadoValores.EnJuego));
        Assert.That(b.Codigo, Is.EqualTo(a.Codigo), "B entra en la sala de A");
        Assert.That(b.Mensaje, Does.Contain("Rival encontrado"));
        Assert.That(e.Salas.Cola.Entradas, Is.Empty, "los dos salen de la cola");
        Assert.That(e.Avisos.Avisos, Has.Some.EqualTo(new Aviso(Escenario.A, a.Codigo)), "A se entera por push");
        Assert.That((e.Salas.ActivaDe(Escenario.A), e.Salas.ActivaDe(Escenario.B)), Is.EqualTo((a.Codigo, a.Codigo)));
        Assert.That((await e.Servicio.GetMatchStateAsync(Escenario.A, a.Codigo, e.Ahora)).Estado, Is.EqualTo(EstadoValores.EnJuego));
    }

    [Test]
    public async Task ConTresJugadores_DosJuegan_YElTerceroEspera()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);
        await Buscar(e, Escenario.B, "F2");
        var c = await Buscar(e, C, "F3");

        Assert.That(c.Estado, Is.EqualTo(EstadoValores.EsperandoRival));
        Assert.That(c.Codigo, Is.Not.EqualTo(a.Codigo));
        Assert.That(e.Salas.Cola.Entradas.Select(x => x.JugadorId), Is.EqualTo(new[] { C }));
    }

    [Test]
    public async Task ConMuchosBuscando_SeFormanParejas_YNuncaHayDosEsperandoALaVez()
    {
        // Cada búsqueda reclama al que espera, así que la cola tiene como mucho una persona: nadie espera
        // teniendo a otro esperando al lado. (El orden "el más antiguo primero" sólo importa en carreras: ver la prueba de la cola.)
        var e = new Escenario();
        var jugadores = Enumerable.Range(1, 7).Select(i => $"jugador-{i}").ToList();
        var salas = new List<string>();
        foreach (var j in jugadores)
        {
            e.Ahora = e.Ahora.AddSeconds(1);
            salas.Add((await Buscar(e, j)).Codigo);
            Assert.That(e.Salas.Cola.Entradas, Has.Count.LessThanOrEqualTo(1), $"tras buscar {j}");
        }

        var porSala = salas.GroupBy(x => x).Select(g => g.Count()).OrderBy(x => x).ToList();
        Assert.That(porSala, Is.EqualTo(new[] { 1, 2, 2, 2 }), "tres parejas y uno esperando");
        Assert.That(e.Salas.Cola.Entradas.Single().JugadorId, Is.EqualTo("jugador-7"));
    }

    [Test]
    public async Task BuscarDeNuevo_DevuelveLaMismaSala_SinDuplicarse()
    {
        var e = new Escenario();
        var primera = await Buscar(e, Escenario.A);
        var segunda = await Buscar(e, Escenario.A);
        var reintento = await e.Servicio.QuickMatchAsync(Escenario.A, "mismo-id", "F1", e.Ahora);
        var otraVez = await e.Servicio.QuickMatchAsync(Escenario.A, "mismo-id", "F1", e.Ahora);

        Assert.That(new[] { segunda.Codigo, reintento.Codigo, otraVez.Codigo }, Is.All.EqualTo(primera.Codigo));
        Assert.That(segunda.Resultado, Is.EqualTo(Resultado.Repetido));
        Assert.That(e.Repo.Cantidad, Is.EqualTo(1), "no se crean salas de más");
        Assert.That(e.Salas.Cola.Entradas, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task QuienCerroElJuego_CaducaEnLaCola_YNoSeLeEmpareja()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);
        e.Ahora = e.Ahora.AddSeconds(Emparejamiento.SegundosEnCola + 1);

        var b = await Buscar(e, Escenario.B, "F2");

        Assert.That(b.Estado, Is.EqualTo(EstadoValores.EsperandoRival), "A ya no cuenta como esperando");
        Assert.That(b.Codigo, Is.Not.EqualTo(a.Codigo));
        Assert.That(e.Salas.Cola.Entradas.Select(x => x.JugadorId), Is.EqualTo(new[] { Escenario.B }));
    }

    [Test]
    public async Task QuienEsperaConElJuegoAbierto_NoCaduca_PorqueSuSondeoLoMantieneEnLaCola()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);
        // A espera 5 minutos; su panel consulta cada 10 s (el sondeo de respaldo).
        for (var s = 0; s < 30; s++)
        {
            e.Ahora = e.Ahora.AddSeconds(10);
            await e.Servicio.GetMatchStateAsync(Escenario.A, a.Codigo, e.Ahora);
        }

        var b = await Buscar(e, Escenario.B, "F2");

        Assert.That((b.Codigo, b.Estado), Is.EqualTo((a.Codigo, EstadoValores.EnJuego)), "A seguía buscando: B se une a él");
    }

    [Test]
    public async Task UnaSalaPorCodigo_NoSeLeOfreceAUnDesconocido()
    {
        var e = new Escenario();
        var deAmigo = await e.Servicio.CreateMatchAsync(Escenario.A, "para-mi-amigo", "F1", e.Ahora);
        await e.Servicio.GetMatchStateAsync(Escenario.A, deAmigo.Codigo, e.Ahora);   // A consulta mientras espera a su amigo

        var b = await Buscar(e, Escenario.B, "F2");

        Assert.That(b.Codigo, Is.Not.EqualTo(deAmigo.Codigo), "la sala de A es para quien tenga el código");
        Assert.That(e.Salas.Cola.Entradas.Select(x => x.JugadorId), Is.EqualTo(new[] { Escenario.B }));
    }

    [Test]
    public async Task SiQuienVuelveTieneSalaVacia_YOtroEspera_SeJuntanEnVezDeEsperarLosDos()
    {
        var e = new Escenario();
        var salaDeA = await Buscar(e, Escenario.A);
        e.Ahora = e.Ahora.AddSeconds(Emparejamiento.SegundosEnCola + 1);   // A caducó en la cola...
        var salaDeB = await Buscar(e, Escenario.B, "F2");                  // ...así que B abrió su propia sala

        var vuelta = await Buscar(e, Escenario.A);                         // A vuelve a buscar

        Assert.That(vuelta.Estado, Is.EqualTo(EstadoValores.EnJuego));
        Assert.That(vuelta.Codigo, Is.EqualTo(salaDeB.Codigo), "A se une a la sala de B");
        var vieja = await e.Servicio.GetMatchStateAsync(Escenario.A, salaDeA.Codigo, e.Ahora);
        Assert.That(vieja.MotivoFin, Is.EqualTo("cancelada"), "la sala vacía de A se cancela");
        Assert.That(e.Salas.Cola.Entradas, Is.Empty);
    }

    [Test]
    public async Task UnaSalaQueYaNoAdmite_SeSaltaYSeDescartaDeLaCola()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);
        // A cancela su sala "a mano" (sin pasar por CancelQuickMatch): su entrada sigue en la cola.
        await e.Servicio.PlayMoveAsync(Escenario.A, a.Codigo, "abandono-directo", -1, Jugada.Propuesta(TipoJugada.Abandonar), e.Ahora);

        var b = await Buscar(e, Escenario.B, "F2");

        Assert.That(b.Estado, Is.EqualTo(EstadoValores.EsperandoRival), "no se une a una sala muerta");
        Assert.That(b.Codigo, Is.Not.EqualTo(a.Codigo));
        Assert.That(e.Salas.Cola.Entradas.Select(x => x.JugadorId), Is.EqualTo(new[] { Escenario.B }));
    }

    // ---------------------------------------------------------------- cancelar

    [Test]
    public async Task Cancelar_SacaDeLaCola_YCancelaLaSalaVacia()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);

        var cancelada = await e.Servicio.CancelQuickMatchAsync(Escenario.A, e.Ahora);

        Assert.That(cancelada.MotivoFin, Is.EqualTo("cancelada"));
        Assert.That(cancelada.Mensaje, Does.Contain("cancelada"));
        Assert.That(e.Salas.Cola.Entradas, Is.Empty);
        Assert.That(e.Salas.ActivaDe(Escenario.A), Is.Empty);
        var b = await Buscar(e, Escenario.B, "F2");
        Assert.That(b.Codigo, Is.Not.EqualTo(a.Codigo), "B ya no puede caer en la sala cancelada");
    }

    [Test]
    public async Task CancelarSinEstarBuscando_NoHaceNada_NiTocaUnaPartidaEmpezada()
    {
        var e = new Escenario();
        var nada = await e.Servicio.CancelQuickMatchAsync(Escenario.A, e.Ahora);
        Assert.That((nada.Codigo, nada.Mensaje), Is.EqualTo(("", "No estabas buscando rival.")));
        Assert.That(nada.Protocolo, Is.EqualTo(GameProtocol.Version));

        var a = await Buscar(e, Escenario.A);
        await Buscar(e, Escenario.B, "F2");
        await e.Servicio.CancelQuickMatchAsync(Escenario.A, e.Ahora);
        var sigue = await e.Servicio.GetMatchStateAsync(Escenario.A, a.Codigo, e.Ahora);
        Assert.That(sigue.Estado, Is.EqualTo(EstadoValores.EnJuego), "cancelar la búsqueda no abandona una partida en juego");
    }

    // ---------------------------------------------------------------- reanudar

    [Test]
    public async Task GetActiveMatch_DevuelveLaPartidaEnCurso_YOlvidaLasTerminadas()
    {
        var e = new Escenario();
        Assert.That((await e.Servicio.GetActiveMatchAsync(Escenario.A, e.Ahora)).Codigo, Is.Empty);

        var a = await Buscar(e, Escenario.A);
        await Buscar(e, Escenario.B, "F2");
        var activa = await e.Servicio.GetActiveMatchAsync(Escenario.A, e.Ahora);
        Assert.That((activa.Codigo, activa.Estado), Is.EqualTo((a.Codigo, EstadoValores.EnJuego)));
        Assert.That(activa.Mensaje, Does.Contain("reanudada"));
        Assert.That(activa.MiMano, Has.Count.EqualTo(5), "es la vista completa, lista para pintar");

        await e.Servicio.PlayMoveAsync(Escenario.B, a.Codigo, "me-voy", -1, Jugada.Propuesta(TipoJugada.Abandonar), e.Ahora);
        Assert.That((await e.Servicio.GetActiveMatchAsync(Escenario.A, e.Ahora)).Codigo, Is.Empty, "una partida terminada ya no es la activa");
        Assert.That(e.Salas.ActivaDe(Escenario.A), Is.Empty, "y su puntero se limpia");
    }

    [Test]
    public async Task GetActiveMatch_AplicaLosPlazosVencidos_ConElRelojDelServidor()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);
        await Buscar(e, Escenario.B, "F2");
        var turnoAntes = (await e.Servicio.GetMatchStateAsync(Escenario.A, a.Codigo, e.Ahora)).JugadorEnTurnoId;
        e.Ahora = e.Ahora.AddSeconds(Config.SegundosPorTurno + 1);

        var reanudada = await e.Servicio.GetActiveMatchAsync(Escenario.A, e.Ahora);

        Assert.That(reanudada.JugadorEnTurnoId, Is.Not.EqualTo(turnoAntes), "el turno vencido pasó al reanudar");
    }

    // ---------------------------------------------------------------- una partida a la vez

    [Test]
    public async Task ConUnaPartidaEnJuego_NoSePuedeCrearNiUnirseAOtra_YBuscarLaReanuda()
    {
        var e = new Escenario();
        var a = await Buscar(e, Escenario.A);
        await Buscar(e, Escenario.B, "F2");
        var salaDeC = await e.Servicio.CreateMatchAsync(C, "sala-c", "F3", e.Ahora);

        var crear = Assert.ThrowsAsync<PartidaException>(() => e.Servicio.CreateMatchAsync(Escenario.A, "otra", "F1", e.Ahora));
        Assert.That(crear!.Message, Does.Contain("Ya tienes una partida en curso").And.Contain(a.Codigo));
        Assert.ThrowsAsync<PartidaException>(() => e.Servicio.JoinMatchAsync(Escenario.A, salaDeC.Codigo, "unirme", "F1", e.Ahora));

        var buscar = await Buscar(e, Escenario.A);
        Assert.That((buscar.Codigo, buscar.Resultado), Is.EqualTo((a.Codigo, Resultado.Repetido)));

        // Al terminar, ya se puede empezar otra.
        await e.Servicio.PlayMoveAsync(Escenario.A, a.Codigo, "rendirse", -1, Jugada.Propuesta(TipoJugada.Abandonar), e.Ahora);
        var nueva = await e.Servicio.CreateMatchAsync(Escenario.A, "nueva", "F1", e.Ahora);
        Assert.That(nueva.Estado, Is.EqualTo(EstadoValores.EsperandoRival));
    }

    // ---------------------------------------------------------------- concurrencia en la cola

    [Test]
    public async Task SiOtraBusquedaEscribeLaColaAntes_SeReintentaSinPerderNada()
    {
        var e = new Escenario();
        e.Salas.FallosDeColaPendientes = 2;   // dos escrituras pierden la carrera

        var a = await Buscar(e, Escenario.A);

        Assert.That(e.Salas.Cola.Entradas.Single().MatchId, Is.EqualTo(a.Codigo));
    }

    [Test]
    public void SiLaColaEstaSaturada_SeAvisaEnVezDeColgarse()
    {
        var e = new Escenario();
        e.Salas.FallosDeColaPendientes = 50;
        var ex = Assert.ThrowsAsync<PartidaException>(() => Buscar(e, Escenario.A));
        Assert.That(ex!.Message, Does.Contain("muy ocupada"));
    }

    // ---------------------------------------------------------------- la lógica pura de la cola

    [Test]
    public void Cola_CandidatoEsElMasAntiguo_NuncaUnoMismo_YCadaJugadorUnaVez()
    {
        var t = Mesa.Hora;
        var c = new ColaEmparejamiento();
        Emparejamiento.Anotar(c, "x", "SALA1", t.AddSeconds(10));
        Emparejamiento.Anotar(c, "y", "SALA2", t);
        Emparejamiento.Anotar(c, "x", "SALA3", t.AddSeconds(20));   // x vuelve a anotarse: sustituye

        Assert.That(c.Entradas, Has.Count.EqualTo(2));
        Assert.That(Emparejamiento.Candidato(c, "z")!.JugadorId, Is.EqualTo("y"), "el más antiguo");
        Assert.That(Emparejamiento.Candidato(c, "y")!.JugadorId, Is.EqualTo("x"), "nunca uno mismo");
        Assert.That(c.Entradas.Single(x => x.JugadorId == "x").MatchId, Is.EqualTo("SALA3"));
    }

    [Test]
    public void Cola_PodarQuitaSoloLasCaducadas()
    {
        var t = Mesa.Hora;
        var c = new ColaEmparejamiento();
        Emparejamiento.Anotar(c, "viejo", "S1", t);
        Emparejamiento.Anotar(c, "nuevo", "S2", t.AddSeconds(100));

        Assert.That(Emparejamiento.Podar(c, t.AddSeconds(Emparejamiento.SegundosEnCola + 1)), Is.EqualTo(1));
        Assert.That(c.Entradas.Single().JugadorId, Is.EqualTo("nuevo"));
    }
}
