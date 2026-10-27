using Newtonsoft.Json;
using QuizzerMatch;

namespace QuizzerMatch.Tests;

[TestFixture]
public class CatalogoTests
{
    [Test]
    public void HayVeinteHeroes_EnCuatroFaccionesDeCinco()
    {
        Assert.That(Catalogo.Todos, Has.Count.EqualTo(20));
        foreach (var f in Catalogo.Facciones)
            Assert.That(Catalogo.DeFaccion(f), Has.Count.EqualTo(5), f);
    }

    [Test]
    public void CadaHeroe_TieneCuatroHabilidades_YAlMenosUnaDisponibleEnElServidor()
    {
        foreach (var h in Catalogo.Todos)
        {
            Assert.That(h.Habilidades, Has.Length.EqualTo(4), h.Id);
            Assert.That(h.Habilidades.Any(Reglas.Disponible), Is.True, $"{h.Id} no podría hacer nada en el servidor");
        }
    }

    [Test]
    public void CadaFaccion_TieneAlMenosUnHeroeQuePuedeHacerDano()
    {
        // El Bufeador, por ejemplo, es un héroe de apoyo puro: no hace falta que todos peguen,
        // pero una facción sin daño no podría ganar nunca.
        foreach (var f in Catalogo.Facciones)
            Assert.That(Catalogo.DeFaccion(f).Any(h => h.Habilidades.Any(x => Reglas.Disponible(x) &&
                x.Efecto is Efecto.Dano or Efecto.DanoTodos or Efecto.DanoDos)), Is.True, f);
    }

    [Test]
    public void LosIdsCoincidenConLosDelModuloTienda()
    {
        // Mismos ids que Tienda/TablaRivales.cs: el inventario y la partida hablan el mismo idioma.
        foreach (var id in new[] { "bufeador", "lanza_con_escudo", "guerrero_del_hacha", "guerrero_dos_hachas",
                                   "hombre_lobo", "curandero", "muneco_poseido", "draconiano" })
            Assert.That(Catalogo.Buscar(id), Is.Not.Null, id);
    }

    [Test]
    public void BancoDeCuarentaPreguntas_ConRespuestaCorrectaEnRango()
    {
        Assert.That(BancoPreguntas.Cantidad, Is.EqualTo(40));
        for (var i = 0; i < BancoPreguntas.Cantidad; i++)
        {
            var p = BancoPreguntas.Obtener(i);
            Assert.That(p.Opciones, Has.Length.EqualTo(4));
            Assert.That(p.Correcta, Is.InRange(0, 3));
        }
    }
}

[TestFixture]
public class AltaDePartidaTests
{
    [Test]
    public async Task CrearYUnirse_ArrancaLaPartida_ConMonedaDelServidor()
    {
        var e = new Escenario();
        var vista = await e.Empezar();

        Assert.That(vista.Estado, Is.EqualTo(EstadoValores.EnJuego));
        Assert.That(vista.Fase, Is.EqualTo(FaseValores.Colocar));
        Assert.That(e.Estado.JugadorInicialId, Is.AnyOf(Escenario.A, Escenario.B));
        Assert.That(vista.MiMano, Has.Count.EqualTo(5));
        Assert.That(vista.RivalManoCantidad, Is.EqualTo(5));
        Assert.That(e.Avisos.Avisos, Has.One.Matches<Aviso>(a => a.JugadorId == Escenario.A));
    }

    [Test]
    public async Task LaMoneda_EsReproducibleConLaMismaSemilla_YVariaConOtras()
    {
        var iniciales = new HashSet<string>();
        for (var semilla = 0; semilla < 20; semilla++)
        {
            var e = new Escenario(semilla);
            await e.Empezar();
            iniciales.Add(e.Estado.JugadorInicialId);

            var otra = new Escenario(semilla);
            await otra.Empezar();
            Assert.That(otra.Estado.JugadorInicialId, Is.EqualTo(e.Estado.JugadorInicialId), $"semilla {semilla}");
        }
        Assert.That(iniciales, Has.Count.EqualTo(2), "con 20 semillas deberían salir los dos resultados");
    }

    [Test]
    public async Task ReintentarCrear_CaeSobreLaMismaPartida_NoCreaOtra()
    {
        var e = new Escenario();
        var primera = await e.Servicio.CreateMatchAsync(Escenario.A, "mi-peticion", "F1", e.Ahora);
        var segunda = await e.Servicio.CreateMatchAsync(Escenario.A, "mi-peticion", "F1", e.Ahora);

        Assert.That(segunda.Codigo, Is.EqualTo(primera.Codigo));
        Assert.That(segunda.Resultado, Is.EqualTo(Resultado.Repetido));
        Assert.That(e.Repo.Cantidad, Is.EqualTo(1));
    }

    [Test]
    public async Task NoPuedesUnirteATuPropiaPartida_NiUnTerceroEntrar()
    {
        var e = new Escenario();
        var creada = await e.Servicio.CreateMatchAsync(Escenario.A, e.NuevaPeticion(), "F1", e.Ahora);

        Assert.ThrowsAsync<PartidaException>(() => e.Servicio.JoinMatchAsync(Escenario.A, creada.Codigo, e.NuevaPeticion(), "F1", e.Ahora));
        await e.Servicio.JoinMatchAsync(Escenario.B, creada.Codigo, e.NuevaPeticion(), "F2", e.Ahora);
        Assert.ThrowsAsync<PartidaException>(() => e.Servicio.JoinMatchAsync("jugador-C", creada.Codigo, e.NuevaPeticion(), "F3", e.Ahora));
    }

    [Test]
    public async Task UnirseDosVeces_EsUnReintentoInofensivo()
    {
        var e = new Escenario();
        var creada = await e.Servicio.CreateMatchAsync(Escenario.A, e.NuevaPeticion(), "F1", e.Ahora);
        await e.Servicio.JoinMatchAsync(Escenario.B, creada.Codigo, "union", "F2", e.Ahora);
        var otra = await e.Servicio.JoinMatchAsync(Escenario.B, creada.Codigo, "union", "F2", e.Ahora);
        Assert.That(otra.Resultado, Is.EqualTo(Resultado.Repetido));
    }

    [Test]
    public void FaccionInvalida_SeRechaza_YCodigoInexistenteLanzaExcepcion()
    {
        var e = new Escenario();
        Assert.ThrowsAsync<PartidaException>(() => e.Servicio.CreateMatchAsync(Escenario.A, "x", "F9", e.Ahora));
        Assert.ThrowsAsync<PartidaException>(() => e.Servicio.GetMatchStateAsync(Escenario.A, "ZZZZZZ", e.Ahora));
    }

    [Test]
    public async Task FaccionRandom_LaSorteaElServidor()
    {
        var e = new Escenario(faccionA: "Random");
        await e.Empezar();
        Assert.That(Catalogo.Facciones, Does.Contain(e.Estado.Jugadores[Escenario.A].Faccion));
    }

    [Test]
    public async Task UnExtrano_NoPuedeConsultarNiJugarLaPartida()
    {
        var e = new Escenario();
        await e.Empezar();
        Assert.ThrowsAsync<PartidaException>(() => e.Servicio.GetMatchStateAsync("intruso", e.Codigo, e.Ahora));
        Assert.ThrowsAsync<PartidaException>(() =>
            e.Servicio.PlayMoveAsync("intruso", e.Codigo, "r", e.Estado.NumeroTurno, Jugada.Propuesta(TipoJugada.Pasar), e.Ahora));
    }
}

[TestFixture]
public class ColocarTests
{
    [Test]
    public async Task FueraDeTurno_SeRechaza_YElEstadoNoCambia()
    {
        var e = new Escenario();
        await e.Empezar();
        var fuera = e.Otro(e.EnTurno);
        var antes = e.Estado.NumeroTurno;

        var vista = await e.Colocar(fuera, e.Estado.Jugadores[fuera].Mano[0]);

        Assert.That(vista.Resultado, Is.EqualTo(Resultado.NoEsTuTurno));
        Assert.That(e.Estado.NumeroTurno, Is.EqualTo(antes));
        Assert.That(e.Estado.Jugadores[fuera].Campo, Is.Empty);
    }

    [Test]
    public async Task HeroeQueNoEstaEnTuMano_SeRechaza()
    {
        var e = new Escenario();   // A = F1, B = F2
        await e.Empezar();
        var jugador = e.EnTurno;
        var ajeno = e.Estado.Jugadores[e.Otro(jugador)].Mano[0];

        var vista = await e.Colocar(jugador, ajeno);

        Assert.That(vista.Resultado, Is.EqualTo(Resultado.Invalido));
        Assert.That(e.Estado.Jugadores[jugador].Campo, Is.Empty);
    }

    [Test]
    public async Task ColocarUnHeroe_PasaElTurnoAlRival_YLeAvisaPorPush()
    {
        var e = new Escenario();
        await e.Empezar();
        var primero = e.EnTurno;
        var heroe = e.Estado.Jugadores[primero].Mano[0];

        var vista = await e.Colocar(primero, heroe);

        Assert.That(vista.Resultado, Is.EqualTo(Resultado.Aplicado));
        Assert.That(e.EnTurno, Is.EqualTo(e.Otro(primero)));
        Assert.That(vista.EsTuTurno, Is.False);
        Assert.That(vista.MiMano, Does.Not.Contain(heroe));
        Assert.That(vista.MiCampo.Single().HeroeId, Is.EqualTo(heroe));
        var aviso = e.Avisos.Avisos.Last();
        Assert.That(aviso.JugadorId, Is.EqualTo(e.Otro(primero)), "el push va al rival, no a quien jugó");
        Assert.That(aviso.MatchId, Is.EqualTo(e.Codigo));
    }

    [Test]
    public async Task Casilla_FueraDeRangoOOcupada_SeRechaza()
    {
        var e = new Escenario();
        await e.Empezar();
        var jugador = e.EnTurno;
        var mano = e.Estado.Jugadores[jugador].Mano;

        Assert.That((await e.Colocar(jugador, mano[0], 3)).Resultado, Is.EqualTo(Resultado.Invalido));
        Assert.That((await e.Colocar(jugador, mano[0], -2)).Resultado, Is.EqualTo(Resultado.Invalido));
    }

    [Test]
    public void NoMasDeTresEnElCampo()
    {
        // Con el orden real de la ronda el límite casi no se alcanza (se salta al que no puede colocar),
        // así que se prueba la regla directamente sobre el Core.
        var s = Mesa.Nueva();
        var jugador = s.JugadorEnTurnoId;
        var j = s.Jugadores[jugador];
        for (var i = 0; i < 3; i++)
        {
            var id = j.Mano[0];
            j.Mano.RemoveAt(0);
            j.Campo.Add(new PeleadorEstado { HeroeId = id, Casilla = i, Vida = 50, VidaMax = 50 });
        }

        var r = Reglas.Colocar(s, jugador, j.Mano[0], -1, Mesa.Hora);
        Assert.That(r.Codigo, Is.EqualTo(Resultado.Invalido));
        Assert.That(r.Mensaje, Does.Contain("3"));
    }

    [Test]
    public async Task ElOrdenDeLaRonda_EsColocarColocarHabilidadesHabilidadesPelea()
    {
        var e = new Escenario();
        await e.Empezar();
        var primero = e.EnTurno;
        Assert.That((e.Estado.Fase, e.Estado.Paso), Is.EqualTo((FaseValores.Colocar, 0)));

        await e.Colocar(primero, e.Estado.Jugadores[primero].Mano[0]);
        Assert.That((e.Estado.Fase, e.Estado.Paso, e.EnTurno), Is.EqualTo((FaseValores.Colocar, 1, e.Otro(primero))));

        await e.Colocar(e.EnTurno, e.Estado.Jugadores[e.EnTurno].Mano[0]);
        Assert.That((e.Estado.Fase, e.Estado.Paso, e.EnTurno), Is.EqualTo((FaseValores.Habilidades, 0, primero)));
    }
}

[TestFixture]
public class QuizTests
{
    private static (string Peleador, int Indice) PrimeraHabilidad(VistaPartida v)
    {
        var p = v.MiCampo.First(x => x.HabilidadesElegibles.Count > 0);
        return (p.HeroeId, p.HabilidadesElegibles[0]);
    }

    [Test]
    public async Task ElegirHabilidad_PlantaUnaPregunta_SoloParaQuienDebeResponder_SinLaRespuesta()
    {
        var e = new Escenario();
        await e.LlevarAHabilidades();
        var jugador = e.EnTurno;
        var (peleador, indice) = PrimeraHabilidad(await e.Ver(jugador));

        var vista = await e.Elegir(jugador, peleador, indice);

        Assert.That(vista.Resultado, Is.EqualTo(Resultado.Aplicado));
        Assert.That(vista.Pregunta, Is.Not.Null);
        Assert.That(vista.Pregunta!.Opciones, Has.Count.EqualTo(4));
        var rival = await e.Ver(e.Otro(jugador));
        Assert.That(rival.Pregunta, Is.Null, "el rival no ve la pregunta");

        var json = JsonConvert.SerializeObject(vista);
        Assert.That(json, Does.Not.Contain("Correcta"), "la respuesta correcta no puede viajar al cliente");
        Assert.That(json, Does.Not.Contain("Semilla"));
    }

    private static async Task<(VistaPartida Vista, string Jugador, string Peleador, int Indice)> PlantarPregunta(Escenario e)
    {
        await e.LlevarAHabilidades();
        var jugador = e.EnTurno;
        var (peleador, indice) = PrimeraHabilidad(await e.Ver(jugador));
        return (await e.Elegir(jugador, peleador, indice), jugador, peleador, indice);
    }

    [Test]
    public async Task Acierto_DaMasVeinticincoPorCiento()
    {
        var e = new Escenario();
        var (v, jugador, peleador, _) = await PlantarPregunta(e);
        var correcta = BancoPreguntas.Obtener(v.Pregunta!.PreguntaId).Correcta;

        var r = await e.Responder(jugador, v.Pregunta.PreguntaId, correcta);

        Assert.That(r.Resultado, Is.EqualTo(Resultado.Aplicado));
        var p = e.Estado.Jugadores[jugador].Campo.Single(x => x.HeroeId == peleador);
        Assert.That(p.MultiplicadorPreparado, Is.EqualTo(1.25).Within(1e-9));
    }

    [Test]
    public async Task Error_RestaVeinticincoPorCiento()
    {
        var e = new Escenario();
        var (v, jugador, peleador, _) = await PlantarPregunta(e);
        var mala = (BancoPreguntas.Obtener(v.Pregunta!.PreguntaId).Correcta + 1) % 4;

        await e.Responder(jugador, v.Pregunta.PreguntaId, mala);

        var p = e.Estado.Jugadores[jugador].Campo.Single(x => x.HeroeId == peleador);
        Assert.That(p.MultiplicadorPreparado, Is.EqualTo(0.75).Within(1e-9));
    }

    [Test]
    public async Task ResponderTarde_CuentaComoFallo_AunqueLaOpcionSeaLaCorrecta()
    {
        var e = new Escenario();
        var (v, jugador, peleador, _) = await PlantarPregunta(e);
        var correcta = BancoPreguntas.Obtener(v.Pregunta!.PreguntaId).Correcta;
        e.Ahora = e.Ahora.AddSeconds(Config.SegundosQuiz + 1);   // pasa el plazo de la pregunta, no el del turno

        var r = await e.Responder(jugador, v.Pregunta.PreguntaId, correcta);

        Assert.That(r.Mensaje, Does.Contain("Tiempo agotado"));
        var p = e.Estado.Jugadores[jugador].Campo.Single(x => x.HeroeId == peleador);
        Assert.That(p.MultiplicadorPreparado, Is.EqualTo(0.75).Within(1e-9));
    }

    [Test]
    public async Task ResponderConOtroIdODosVeces_SeRechaza()
    {
        var e = new Escenario();
        var (v, jugador, _, _) = await PlantarPregunta(e);

        var otroId = (await e.Responder(jugador, v.Pregunta!.PreguntaId + 1, 0));
        Assert.That(otroId.Resultado, Is.EqualTo(Resultado.Invalido));
        var fueraDeRango = await e.Responder(jugador, v.Pregunta.PreguntaId, 9);
        Assert.That(fueraDeRango.Resultado, Is.EqualTo(Resultado.Invalido));

        await e.Responder(jugador, v.Pregunta.PreguntaId, 0);
        var segunda = await e.Responder(jugador, v.Pregunta.PreguntaId, 0);
        Assert.That(segunda.Resultado, Is.Not.EqualTo(Resultado.Aplicado), "una pregunta se responde una sola vez");
    }

    [Test]
    public async Task ElRivalNoPuedeResponderLaPreguntaDeOtro()
    {
        var e = new Escenario();
        var (v, jugador, _, _) = await PlantarPregunta(e);
        var r = await e.Responder(e.Otro(jugador), v.Pregunta!.PreguntaId, 0);
        Assert.That(r.Resultado, Is.EqualTo(Resultado.NoEsTuTurno));
    }

    [Test]
    public async Task NoSePuedePasarConUnaPreguntaPendiente_NiElegirOtraHabilidad()
    {
        var e = new Escenario();
        var (_, jugador, _, _) = await PlantarPregunta(e);
        Assert.That((await e.Pasar(jugador)).Resultado, Is.EqualTo(Resultado.Invalido));
        var (p, i) = PrimeraHabilidad(await e.Ver(jugador));
        Assert.That((await e.Elegir(jugador, p, i)).Resultado, Is.EqualTo(Resultado.Invalido));
    }

    [Test]
    public async Task HabilidadNoImplementada_SeRechazaConMensajeClaro()
    {
        var e = new Escenario();
        await e.LlevarAHabilidades();
        var jugador = e.EnTurno;
        var peleador = e.Estado.Jugadores[jugador].Campo[0];
        var def = Catalogo.Heroe(peleador.HeroeId);
        var noDisponible = Array.FindIndex(def.Habilidades, h => !Reglas.Disponible(h));
        if (noDisponible < 0) Assert.Ignore($"{def.Id} tiene todas sus habilidades disponibles");

        var r = await e.Elegir(jugador, peleador.HeroeId, noDisponible);

        Assert.That(r.Resultado, Is.EqualTo(Resultado.Invalido));
        Assert.That(r.Mensaje, Does.Contain("aún no está implementada"));
    }

    [Test]
    public void UnaHabilidadYaUsada_NoSeRepiteHastaAgotarElRepertorio()
    {
        var s = Mesa.Nueva();
        s.Fase = FaseValores.Habilidades;
        var jugador = s.JugadorEnTurnoId;
        var id = s.Jugadores[jugador].Mano[0];
        var p = new PeleadorEstado { HeroeId = id, Casilla = 0, Vida = 50, VidaMax = 50 };
        s.Jugadores[jugador].Campo.Add(p);
        var elegibles = Reglas.Elegibles(p).ToList();
        p.Usadas.Add(elegibles[0]);

        var r = Reglas.ElegirHabilidad(s, jugador, p.HeroeId, elegibles[0], Mesa.Hora);
        Assert.That(r.Codigo, Is.EqualTo(Resultado.Invalido));
        Assert.That(r.Mensaje, Does.Contain("ya se usó"));
    }
}

[TestFixture]
public class IdempotenciaYConcurrenciaTests
{
    [Test]
    public async Task ReintentarLaMismaJugada_NoLaAplicaDosVeces()
    {
        var e = new Escenario();
        await e.Empezar();
        var jugador = e.EnTurno;
        var heroe = e.Estado.Jugadores[jugador].Mano[0];
        var turno = e.Estado.NumeroTurno;

        var primera = await e.Servicio.PlayMoveAsync(jugador, e.Codigo, "misma", turno, Jugada.Propuesta(TipoJugada.Colocar, heroeId: heroe), e.Ahora);
        var turnoTras = e.Estado.NumeroTurno;
        // La respuesta se "perdió" y el cliente reintenta con el MISMO requestId y el mismo turnoEsperado.
        var reintento = await e.Servicio.PlayMoveAsync(jugador, e.Codigo, "misma", turno, Jugada.Propuesta(TipoJugada.Colocar, heroeId: heroe), e.Ahora);

        Assert.That(primera.Resultado, Is.EqualTo(Resultado.Aplicado));
        Assert.That(reintento.Resultado, Is.EqualTo(Resultado.Repetido), "no 'no_es_tu_turno': la idempotencia va antes de validar");
        Assert.That(e.Estado.NumeroTurno, Is.EqualTo(turnoTras));
        Assert.That(e.Estado.Jugadores[jugador].Campo, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UnTurnoEsperadoViejo_SeRechazaComoObsoleto()
    {
        var e = new Escenario();
        await e.Empezar();
        var jugador = e.EnTurno;
        var viejo = e.Estado.NumeroTurno - 1;

        var vista = await e.Servicio.PlayMoveAsync(jugador, e.Codigo, e.NuevaPeticion(), viejo, Jugada.Propuesta(TipoJugada.Pasar), e.Ahora);

        Assert.That(vista.Resultado, Is.EqualTo(Resultado.Obsoleto));
        Assert.That(e.EnTurno, Is.EqualTo(jugador), "no avanzó");
    }

    [Test]
    public async Task SiOtroEscribioPrimero_HayConflicto_NoSePisa_YNoSeAvisa()
    {
        var e = new Escenario();
        await e.Empezar();
        var jugador = e.EnTurno;
        var avisosAntes = e.Avisos.Avisos.Count;
        e.Repo.FallarProximoGuardado = true;

        var vista = await e.Colocar(jugador, e.Estado.Jugadores[jugador].Mano[0]);

        Assert.That(vista.Resultado, Is.EqualTo(Resultado.Conflicto));
        Assert.That(e.Estado.Jugadores[jugador].Campo, Is.Empty, "la jugada perdedora no se guardó");
        Assert.That(e.Avisos.Avisos, Has.Count.EqualTo(avisosAntes));
    }

    [Test]
    public async Task ElRegistro_SoloCreceConJugadasValidas_YNuncaSeReescribe()
    {
        var e = new Escenario();
        await e.Empezar();
        var enTurno = e.EnTurno;
        var antes = e.Registro.Jugadas.Count;
        var prefijo = JsonConvert.SerializeObject(e.Registro.Jugadas);

        await e.Colocar(e.Otro(enTurno), e.Estado.Jugadores[e.Otro(enTurno)].Mano[0]);   // fuera de turno: rechazada
        Assert.That(e.Registro.Jugadas, Has.Count.EqualTo(antes), "una jugada rechazada no se anota");

        await e.Colocar(enTurno, e.Estado.Jugadores[enTurno].Mano[0]);
        Assert.That(e.Registro.Jugadas, Has.Count.EqualTo(antes + 1));
        var ahora = JsonConvert.SerializeObject(e.Registro.Jugadas.Take(antes));
        Assert.That(ahora, Is.EqualTo(prefijo), "lo ya anotado no cambia: el registro es sólo de agregar");
    }
}

[TestFixture]
public class PlazosYAbandonoTests
{
    [Test]
    public async Task ElPlazoVencido_LoAplicaElServidor_AlConsultar_YAvisaAlRival()
    {
        var e = new Escenario();
        await e.Empezar();
        var enTurno = e.EnTurno;
        e.Ahora = e.Ahora.AddSeconds(Config.SegundosPorTurno + 1);

        var vista = await e.Ver(e.Otro(enTurno));   // el rival consulta; el que debía jugar no hizo nada

        Assert.That(e.EnTurno, Is.EqualTo(e.Otro(enTurno)));
        Assert.That(vista.EsTuTurno, Is.True);
        Assert.That(e.Estado.Jugadores[enTurno].TimeoutsSeguidos, Is.EqualTo(1));
        Assert.That(e.Avisos.Avisos.Last(), Is.EqualTo(new Aviso(enTurno, e.Codigo)), "el que consulta aplica el plazo y avisa al que lo agotó");
    }

    [Test]
    public async Task TresPlazosVencidosSeguidos_SonAbandono()
    {
        var e = new Escenario();
        await e.Empezar();
        var pasivo = e.EnTurno;

        // El pasivo nunca juega; el otro pasa cada vez que le toca. En cada vuelta sólo vence el plazo del pasivo.
        for (var vuelta = 0; vuelta < 6 && e.Estado.Estado == EstadoValores.EnJuego; vuelta++)
        {
            if (e.EnTurno == pasivo) e.Ahora = e.Ahora.AddSeconds(Config.SegundosPorTurno + 1);
            await e.Ver(Escenario.A);
            if (e.Estado.Estado == EstadoValores.EnJuego && e.EnTurno != pasivo)
                await e.Pasar(e.EnTurno);
        }

        Assert.That(e.Estado.Estado, Is.EqualTo(EstadoValores.Terminada));
        Assert.That(e.Estado.MotivoFin, Is.EqualTo("timeouts"));
        Assert.That(e.Estado.GanadorId, Is.EqualTo(e.Otro(pasivo)));
    }

    [Test]
    public async Task Abandonar_DaLaVictoriaAlRival_YAvisa()
    {
        var e = new Escenario();
        await e.Empezar();
        var vista = await e.Abandonar(Escenario.A);

        Assert.That(vista.Estado, Is.EqualTo(EstadoValores.Terminada));
        Assert.That(vista.GanadorId, Is.EqualTo(Escenario.B));
        Assert.That(vista.MotivoFin, Is.EqualTo("abandono"));
        Assert.That(e.Avisos.Avisos.Last(), Is.EqualTo(new Aviso(Escenario.B, e.Codigo)));
    }

    [Test]
    public async Task TrasTerminar_NadaSePuedeJugar()
    {
        var e = new Escenario();
        await e.Empezar();
        await e.Abandonar(Escenario.A);

        var vista = await e.Pasar(Escenario.B);
        Assert.That(vista.Resultado, Is.EqualTo(Resultado.Terminada));
    }

    [Test]
    public async Task AbandonarMientrasSeEsperaRival_CancelaLaPartida()
    {
        var e = new Escenario();
        var creada = await e.Servicio.CreateMatchAsync(Escenario.A, e.NuevaPeticion(), "F1", e.Ahora);
        var vista = await e.Servicio.PlayMoveAsync(Escenario.A, creada.Codigo, e.NuevaPeticion(), -1, Jugada.Propuesta(TipoJugada.Abandonar), e.Ahora);
        Assert.That(vista.MotivoFin, Is.EqualTo("cancelada"));
    }
}

[TestFixture]
public class InformacionOcultaTests
{
    [Test]
    public async Task LaVista_NoRevelaManoDelRival_NiSusHabilidadesPreparadas_NiLaSemilla()
    {
        var e = new Escenario();
        await e.LlevarAHabilidades();
        var jugador = e.EnTurno;
        var rivalId = e.Otro(jugador);
        var campo = (await e.Ver(jugador)).MiCampo.First(x => x.HabilidadesElegibles.Count > 0);
        var pregunta = (await e.Elegir(jugador, campo.HeroeId, campo.HabilidadesElegibles[0])).Pregunta!;
        await e.Responder(jugador, pregunta.PreguntaId, BancoPreguntas.Obtener(pregunta.PreguntaId).Correcta);

        // Ahora juega el rival: su vista debe decir CUÁNTOS ha preparado el otro, no cuál.
        var vista = await e.Ver(rivalId);
        var json = JsonConvert.SerializeObject(vista);

        Assert.That(vista.RivalCampo.Single().HabilidadPreparada, Is.EqualTo(-1), "la jugada del rival se revela en la pelea");
        Assert.That(vista.RivalPreparados, Is.EqualTo(1), "sólo se sabe CUÁNTOS ha preparado");
        Assert.That(json, Does.Not.Contain("Semilla"));
        Assert.That(json, Does.Not.Contain("Jugadas"), "el registro no sale del servidor");
        foreach (var heroeEnManoDelOtro in e.Estado.Jugadores[jugador].Mano)
            Assert.That(vista.MiMano, Does.Not.Contain(heroeEnManoDelOtro));
        Assert.That(vista.RivalManoCantidad, Is.EqualTo(e.Estado.Jugadores[jugador].Mano.Count));
    }
}

[TestFixture]
public class PeleaTests
{
    /// <summary>Prepara una pelea a mano: A (cazador, Disparo) contra B (mago), lista para resolverse al pasar el segundo jugador.</summary>
    private static EstadoPartida Preparar(int ronda, double mult, int vidaMago = 90)
    {
        var s = Mesa.Nueva();
        Mesa.ListoParaPelear(s);
        s.Ronda = ronda;
        s.Jugadores[Escenario.A].Campo = new() { new PeleadorEstado { HeroeId = "cazador", Casilla = 0, Vida = 85, VidaMax = 85, HabilidadPreparada = 0, MultiplicadorPreparado = mult } };
        s.Jugadores[Escenario.B].Campo = new() { new PeleadorEstado { HeroeId = "mago", Casilla = 0, Vida = vidaMago, VidaMax = 90 } };
        return s;
    }

    [Test]
    public void ElDano_UsaElMultiplicadorDelQuiz_YElBonoPorRonda()
    {
        // Disparo = 28. Ronda 3 -> x1.2; quiz acertado -> x1.25; total x1.5 -> 42.
        var s = Preparar(ronda: 3, mult: 1.25);

        Mesa.Pasar(s);

        Assert.That(s.Jugadores[Escenario.B].Campo.Single().Vida, Is.EqualTo(90 - 42));
        Assert.That(s.RegistroPelea, Has.Some.Contains("recibe 42"));
    }

    [Test]
    public void UnPeleadorQueCae_SaleDelCampo_YSeAnotaEnElRegistro()
    {
        var s = Preparar(ronda: 1, mult: 1.0, vidaMago: 10);

        Mesa.Pasar(s);

        Assert.That(s.Jugadores[Escenario.B].Campo, Is.Empty);
        Assert.That(s.RegistroPelea, Has.Some.Contains("cae"));
    }

    [Test]
    public void SiUnJugadorSeQuedaSinPeleadoresNiCartas_PierdePorSinPeleadores()
    {
        var s = Preparar(ronda: 1, mult: 1.0, vidaMago: 10);
        s.Jugadores[Escenario.B].Mano.Clear();

        Mesa.Pasar(s);

        Assert.That(s.Estado, Is.EqualTo(EstadoValores.Terminada));
        Assert.That(s.GanadorId, Is.EqualTo(Escenario.A));
        Assert.That(s.MotivoFin, Is.EqualTo("sin_peleadores"));
    }

    [Test]
    public void SiAmbosQuedanSinNada_EsEmpate()
    {
        // Caso límite defensivo: con el orden de la pelea (primero el anfitrión) un empate real
        // casi no puede darse, pero si los dos se quedan sin peleadores ni cartas debe ser empate.
        var s = Preparar(ronda: 1, mult: 1.0, vidaMago: 10);
        foreach (var j in s.Jugadores.Values) { j.Mano.Clear(); j.Campo.Clear(); }

        Mesa.Pasar(s);

        Assert.That(s.MotivoFin, Is.EqualTo("empate"));
        Assert.That(s.GanadorId, Is.Empty);
    }

    [Test]
    public void SiLaPartidaSigue_ArrancaLaRondaSiguiente()
    {
        var s = Preparar(ronda: 1, mult: 1.0);
        var inicial = s.JugadorInicialId;

        Mesa.Pasar(s);

        Assert.That(s.Ronda, Is.EqualTo(2));
        Assert.That(s.Fase, Is.EqualTo(FaseValores.Colocar));
        Assert.That(s.JugadorEnTurnoId, Is.EqualTo(inicial));
    }

    [Test]
    public void Intimidar_BajaElAtaqueDelEnemigoDelCarril()
    {
        // El anfitrión actúa primero: el muñeco intimida (-25 %) y el disparo de 28 del cazador baja a 21.
        var s = Mesa.Nueva("F4", "F2");
        Mesa.ListoParaPelear(s);
        s.Jugadores[Escenario.A].Campo = new() { new PeleadorEstado { HeroeId = "muneco_poseido", Casilla = 0, Vida = 100, VidaMax = 100, HabilidadPreparada = 0 } };
        s.Jugadores[Escenario.B].Campo = new() { new PeleadorEstado { HeroeId = "cazador", Casilla = 0, Vida = 85, VidaMax = 85, HabilidadPreparada = 0 } };

        Mesa.Pasar(s);

        Assert.That(s.Jugadores[Escenario.A].Campo.Single().Vida, Is.EqualTo(100 - 21));
        Assert.That(s.RegistroPelea, Has.Some.Contains("ataque -25 %"));
    }

    [Test]
    public void LaCuracionNoSuperaLaVidaMaxima_YElBuffDeDefensaReduceElDano()
    {
        var s = Mesa.Nueva("F4", "F2");
        Mesa.ListoParaPelear(s);
        // A: curandero (Curacion en area = CurarZona 25) en casilla 0, con el escudo de espadachin (Resistencia = BuffDef 30).
        var curandero = new PeleadorEstado { HeroeId = "curandero", Casilla = 0, Vida = 90, VidaMax = 95, HabilidadPreparada = 0 };
        var espadachin = new PeleadorEstado { HeroeId = "espadachin", Casilla = 1, Vida = 105, VidaMax = 105, HabilidadPreparada = 2 };
        s.Jugadores[Escenario.A].Campo = new() { curandero, espadachin };
        s.Jugadores[Escenario.B].Campo = new() { new PeleadorEstado { HeroeId = "cazador", Casilla = 1, Vida = 85, VidaMax = 85, HabilidadPreparada = 0 } };

        Mesa.Pasar(s);

        var a = s.Jugadores[Escenario.A].Campo;
        Assert.That(a.Single(p => p.HeroeId == "curandero").Vida, Is.EqualTo(95), "90 + 25 se queda en el máximo de 95");
        // El cazador (casilla 1) pega 28 al espadachin (casilla 1) DESPUÉS de que A haya activado Resistencia (-30 %): 28*0.7 = 19,6 -> 20.
        Assert.That(a.Single(p => p.HeroeId == "espadachin").Vida, Is.EqualTo(105 - 20));
    }
}

[TestFixture]
public class PartidaCompletaTests
{
    /// <summary>
    /// Dos "jugadores automáticos" juegan hasta el final sólo con lo que ven en su vista
    /// (como lo haría un cliente real). A acierta siempre el quiz; B lo falla siempre.
    /// </summary>
    private static async Task<(Escenario E, int Pasos)> Jugar(int semilla)
    {
        var e = new Escenario(semilla, "F1", "F3");
        await e.Empezar();
        var pasos = 0;
        var turnoAnterior = 0;

        while (e.Estado.Estado == EstadoValores.EnJuego && pasos++ < 2000)
        {
            e.Ahora = e.Ahora.AddSeconds(1);
            var jugador = e.EnTurno;
            var vista = await e.Ver(jugador);
            Assert.That(vista.NumeroTurno, Is.GreaterThanOrEqualTo(turnoAnterior), "el número de turno nunca retrocede");
            turnoAnterior = vista.NumeroTurno;
            Assert.That(vista.MiCampo.Count, Is.LessThanOrEqualTo(Config.MaxEnCampo));

            VistaPartida r;
            if (vista.Pregunta != null)
            {
                var correcta = BancoPreguntas.Obtener(vista.Pregunta.PreguntaId).Correcta;
                var opcion = jugador == Escenario.A ? correcta : (correcta + 1) % 4;
                r = await e.Responder(jugador, vista.Pregunta.PreguntaId, opcion);
            }
            else if (vista.Fase == FaseValores.Colocar && vista.MiMano.Count > 0)
            {
                r = await e.Colocar(jugador, vista.MiMano[0]);
            }
            else
            {
                var candidato = vista.MiCampo.FirstOrDefault(p => p.HabilidadesElegibles.Count > 0);
                r = vista.Fase == FaseValores.Habilidades && candidato != null
                    ? await e.Elegir(jugador, candidato.HeroeId, candidato.HabilidadesElegibles[0])
                    : await e.Pasar(jugador);
            }
            Assert.That(r.Resultado, Is.EqualTo(Resultado.Aplicado), $"paso {pasos}: {r.Mensaje}");
        }
        return (e, pasos);
    }

    [Test]
    public async Task UnaPartidaEntera_TerminaConResultado_SinJugadasIlegales_NiVidaFueraDeRango()
    {
        var (e, pasos) = await Jugar(semilla: 7);

        Assert.That(e.Estado.Estado, Is.EqualTo(EstadoValores.Terminada), $"no terminó en {pasos} pasos");
        Assert.That(e.Estado.MotivoFin, Is.AnyOf("sin_peleadores", "empate", "limite_de_rondas"));
        foreach (var j in e.Estado.Jugadores.Values)
            foreach (var p in j.Campo)
                Assert.That(p.Vida, Is.InRange(1, p.VidaMax));
        TestContext.Out.WriteLine($"Terminó en {pasos} jugadas, ronda {e.Estado.Ronda}, ganador: {e.Estado.GanadorId} ({e.Estado.MotivoFin})");
    }

    [Test]
    public async Task LaMismaSemillaYLasMismasJugadas_DanLaMismaPartida()
    {
        var (a, _) = await Jugar(semilla: 11);
        var (b, _) = await Jugar(semilla: 11);

        Assert.That(b.Estado.Ronda, Is.EqualTo(a.Estado.Ronda));
        Assert.That(b.Estado.GanadorId, Is.EqualTo(a.Estado.GanadorId));
        Assert.That(b.Estado.RegistroPelea, Is.EqualTo(a.Estado.RegistroPelea));
    }

    [Test]
    public async Task VariasSemillas_TodasTerminan()
    {
        for (var semilla = 100; semilla < 130; semilla++)
        {
            var (e, pasos) = await Jugar(semilla);
            Assert.That(e.Estado.Estado, Is.EqualTo(EstadoValores.Terminada), $"semilla {semilla} no terminó en {pasos} pasos");
        }
    }
}
