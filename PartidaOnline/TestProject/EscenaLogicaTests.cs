using Newtonsoft.Json;
using QuizzerMatch;
using Cliente = QuizzerAttack.Partida;
using Logica = QuizzerAttack.Partida.EscenaLogica;

namespace QuizzerMatch.Tests;

/// <summary>
/// La lógica del modo online en la escena real (Assets/Scripts/Online/Partida/EscenaLogica.cs, compilada aquí).
/// Se alimenta con vistas REALES del servidor, pasadas por JSON hacia el DTO del cliente como en producción.
/// </summary>
[TestFixture]
public class EscenaLogicaTests
{
    // Las diez cartas que hay en la escena "campo de batalla": nombre, facción y mano (jugadorDueno).
    private static readonly (string Nombre, string Faccion, int Dueno)[] CartasDeLaEscena =
    {
        ("Asesino", "F1", 1), ("Bufeador", "F1", 1), ("Enano", "F1", 1), ("Lanza con escudo", "F1", 1), ("Vampiro", "F1", 1),
        ("Cazador", "F2", 2), ("Guerrero del hacha", "F2", 2), ("Lancero", "F2", 2), ("Mago", "F2", 2), ("Nigromante", "F2", 2),
    };

    private static Cliente.VistaPartidaDto Dto(VistaPartida v) =>
        JsonConvert.DeserializeObject<Cliente.VistaPartidaDto>(JsonConvert.SerializeObject(v))!;

    private static List<Logica.CartaEscena> Cartas() =>
        CartasDeLaEscena.Select(c => new Logica.CartaEscena { Faccion = c.Faccion, JugadorDueno = c.Dueno }).ToList();

    /// <summary>Lo que haría EscenaOnline con las acciones, sobre una escena simulada.</summary>
    private static void Aplicar(List<Logica.PeleadorEscena> escena, IEnumerable<Logica.Accion> acciones)
    {
        foreach (var a in acciones)
        {
            switch (a.Tipo)
            {
                case Logica.TipoAccion.Colocar:
                    escena.Add(new Logica.PeleadorEscena { Lado = a.Lado, HeroeId = a.HeroeId, Casilla = a.Casilla, Vida = a.Vida, VidaMax = a.VidaMax, Preparada = a.Preparada });
                    break;
                case Logica.TipoAccion.Actualizar:
                    var p = escena.Single(e => e.Lado == a.Lado && e.HeroeId == a.HeroeId);
                    p.Vida = a.Vida; p.VidaMax = a.VidaMax; p.Preparada = a.Preparada;
                    break;
                case Logica.TipoAccion.Quitar:
                    escena.RemoveAll(e => e.Lado == a.Lado && e.HeroeId == a.HeroeId);
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- el vínculo entre escena y servidor

    [Test]
    public void LasDiezCartasDeLaEscena_TienenUnHeroeDelServidor_DeLaMismaFaccion()
    {
        foreach (var (nombre, faccion, _) in CartasDeLaEscena)
        {
            var heroe = Catalogo.Buscar(Logica.NombreAId(nombre));
            Assert.That(heroe, Is.Not.Null, $"'{nombre}' -> '{Logica.NombreAId(nombre)}' no existe en el servidor");
            Assert.That(heroe!.Faccion, Is.EqualTo(faccion), nombre);
        }
    }

    [Test]
    public void NombreAId_NormalizaEspaciosYMayusculas()
    {
        Assert.That(Logica.NombreAId("Guerrero del hacha"), Is.EqualTo("guerrero_del_hacha"));
        Assert.That(Logica.NombreAId("  Lanza con escudo "), Is.EqualTo("lanza_con_escudo"));
        Assert.That(Logica.NombreAId(null), Is.Empty);
    }

    [Test]
    public void Lados_CadaJugadorUsaLaManoDeSuFaccion_ConUnMensajeSiNoSePuede()
    {
        Assert.That(Logica.ResolverLados("F1", "F2", Cartas(), out var mio, out var rival, out var error), Is.True);
        Assert.That((mio, rival, error), Is.EqualTo((1, 2, "")));

        Assert.That(Logica.ResolverLados("F2", "F1", Cartas(), out mio, out rival, out _), Is.True);
        Assert.That((mio, rival), Is.EqualTo((2, 1)));

        Assert.That(Logica.ResolverLados("F1", "F1", Cartas(), out _, out _, out error), Is.False);
        Assert.That(error, Does.Contain("misma facción"));

        Assert.That(Logica.ResolverLados("F3", "F1", Cartas(), out _, out _, out error), Is.False);
        Assert.That(error, Does.Contain("F1 y F2"));

        Assert.That(Logica.ResolverLados("F1", "", Cartas(), out _, out _, out _), Is.False, "aún no se ha unido el rival");
    }

    // ---------------------------------------------------------------- qué cambiar en el campo

    [Test]
    public async Task UnaJugadaDelServidor_SePintaEnLaMano_YEnLaDelRival()
    {
        var e = new Escenario();   // A = F1, B = F2
        await e.Empezar();
        var primero = e.EnTurno;
        await e.Colocar(primero, e.Estado.Jugadores[primero].Mano[0]);

        var vistaA = Dto(await e.Ver(Escenario.A));
        var vistaB = Dto(await e.Ver(Escenario.B));
        var escenaA = new List<Logica.PeleadorEscena>();
        var escenaB = new List<Logica.PeleadorEscena>();

        // Desde A: A es la mano 1 (F1) y B la 2 (F2). Desde B, al revés.
        var accionesA = Logica.Planificar(vistaA, 1, 2, escenaA);
        var accionesB = Logica.Planificar(vistaB, 2, 1, escenaB);

        var ladoDeQuienJugo = primero == Escenario.A ? 1 : 2;
        Assert.That(accionesA, Has.Count.EqualTo(1));
        Assert.That(accionesA[0].Tipo, Is.EqualTo(Logica.TipoAccion.Colocar));
        Assert.That(accionesA[0].Lado, Is.EqualTo(ladoDeQuienJugo), "el héroe aparece en la mano de quien lo colocó");
        Assert.That(accionesB, Has.Count.EqualTo(1));
        Assert.That(accionesB[0].Lado, Is.EqualTo(ladoDeQuienJugo), "los dos clientes pintan el mismo héroe en el mismo lado");
        Assert.That(accionesA[0].HeroeId, Is.EqualTo(accionesB[0].HeroeId));
        Assert.That(accionesA[0].Casilla, Is.EqualTo(accionesB[0].Casilla), "la casilla la decide el servidor, no el cliente");
    }

    [Test]
    public void LaVidaSeActualiza_YLosCaidosSeQuitan()
    {
        var s = Mesa.Nueva();    // A = F2 ... se usa sólo el campo
        s.Jugadores[Escenario.A].Campo = new() {
            new PeleadorEstado { HeroeId = "cazador", Casilla = 0, Vida = 40, VidaMax = 85 },
        };
        s.Jugadores[Escenario.B].Campo = new() {
            new PeleadorEstado { HeroeId = "mago", Casilla = 0, Vida = 90, VidaMax = 90 },
        };
        var vista = Dto(Vistas.Construir(s, Escenario.A, "ok", "", Mesa.Hora));
        var escena = new List<Logica.PeleadorEscena>
        {
            new() { Lado = 1, HeroeId = "cazador", Casilla = 0, Vida = 85, VidaMax = 85 },    // en la escena sigue entero
            new() { Lado = 2, HeroeId = "mago", Casilla = 0, Vida = 90, VidaMax = 90 },
            new() { Lado = 2, HeroeId = "nigromante", Casilla = 1, Vida = 5, VidaMax = 90 },   // cayó: el servidor ya no lo tiene
        };

        var acciones = Logica.Planificar(vista, 1, 2, escena);

        Assert.That(acciones.Single(a => a.Tipo == Logica.TipoAccion.Actualizar).Vida, Is.EqualTo(40));
        var quitar = acciones.Single(a => a.Tipo == Logica.TipoAccion.Quitar);
        Assert.That((quitar.Lado, quitar.HeroeId), Is.EqualTo((2, "nigromante")));
        Assert.That(acciones, Has.Count.EqualTo(2), "el mago no cambió: no hay acción");
    }

    [Test]
    public void LaHabilidadPreparada_SoloSePintaEnLosPropios()
    {
        var s = Mesa.Nueva();
        s.Jugadores[Escenario.A].Campo = new() { new PeleadorEstado { HeroeId = "cazador", Casilla = 0, Vida = 85, VidaMax = 85, HabilidadPreparada = 0 } };
        s.Jugadores[Escenario.B].Campo = new() { new PeleadorEstado { HeroeId = "mago", Casilla = 0, Vida = 90, VidaMax = 90, HabilidadPreparada = 1 } };
        var vista = Dto(Vistas.Construir(s, Escenario.A, "ok", "", Mesa.Hora));
        var escena = new List<Logica.PeleadorEscena>
        {
            new() { Lado = 1, HeroeId = "cazador", Vida = 85, VidaMax = 85, Preparada = false },
            new() { Lado = 2, HeroeId = "mago", Vida = 90, VidaMax = 90, Preparada = false },
        };

        var acciones = Logica.Planificar(vista, 1, 2, escena);

        Assert.That(acciones, Has.Count.EqualTo(1), "el rival tiene una habilidad preparada, pero el servidor no dice cuál ni se pinta");
        Assert.That((acciones[0].Lado, acciones[0].HeroeId, acciones[0].Preparada), Is.EqualTo((1, "cazador", true)));
    }

    [Test]
    public async Task UnaPartidaEntera_LaEscenaSimuladaConvergeConElServidor_EnLosDosClientes()
    {
        // Dos jugadores automáticos juegan hasta el final. Tras CADA jugada, los dos clientes (A = mano 1, B = mano 2
        // y viceversa) aplican las acciones a su escena y debe quedar igual que el servidor y estable (sin más acciones).
        var e = new Escenario(semilla: 21, faccionA: "F1", faccionB: "F2");
        await e.Empezar();
        var escenaA = new List<Logica.PeleadorEscena>();
        var escenaB = new List<Logica.PeleadorEscena>();

        for (var paso = 0; paso < 600 && e.Estado.Estado == EstadoValores.EnJuego; paso++)
        {
            e.Ahora = e.Ahora.AddSeconds(1);
            var jugador = e.EnTurno;
            var vista = await e.Ver(jugador);
            VistaPartida r;
            if (vista.Pregunta != null)
                r = await e.Responder(jugador, vista.Pregunta.PreguntaId, BancoPreguntas.Obtener(vista.Pregunta.PreguntaId).Correcta);
            else if (vista.Fase == FaseValores.Colocar && vista.MiMano.Count > 0)
                r = await e.Colocar(jugador, vista.MiMano[0]);
            else
            {
                var c = vista.MiCampo.FirstOrDefault(p => p.HabilidadesElegibles.Count > 0);
                r = vista.Fase == FaseValores.Habilidades && c != null
                    ? await e.Elegir(jugador, c.HeroeId, c.HabilidadesElegibles[0])
                    : await e.Pasar(jugador);
            }
            Assert.That(r.Resultado, Is.EqualTo(Resultado.Aplicado), r.Mensaje);

            foreach (var (jug, escena, mio, rival) in new[] { (Escenario.A, escenaA, 1, 2), (Escenario.B, escenaB, 2, 1) })
            {
                var dto = Dto(await e.Ver(jug));
                Aplicar(escena, Logica.Planificar(dto, mio, rival, escena));

                Assert.That(Logica.Planificar(dto, mio, rival, escena), Is.Empty, $"paso {paso}: la escena de {jug} no es estable");
                Assert.That(escena.Count(x => x.Lado == mio), Is.EqualTo(dto.MiCampo.Count), $"paso {paso} ({jug}): mi campo");
                Assert.That(escena.Count(x => x.Lado == rival), Is.EqualTo(dto.RivalCampo.Count), $"paso {paso} ({jug}): campo rival");
                Assert.That(escena.Where(x => x.Lado == mio).Select(x => (x.HeroeId, x.Vida)),
                    Is.EquivalentTo(dto.MiCampo.Select(p => (p.HeroeId, p.Vida))), $"paso {paso} ({jug}): vidas");
            }
        }
        Assert.That(e.Estado.Estado, Is.EqualTo(EstadoValores.Terminada), "la partida simulada debe terminar");
        // Los lados son absolutos (mano 1 = F1, mano 2 = F2) en los dos clientes: deben ver exactamente lo mismo.
        Assert.That(escenaA.Select(x => (x.Lado, x.HeroeId, x.Vida)).OrderBy(x => x),
            Is.EqualTo(escenaB.Select(x => (x.Lado, x.HeroeId, x.Vida)).OrderBy(x => x)));
    }

    // ---------------------------------------------------------------- qué puede hacer el jugador

    [Test]
    public async Task LosBotonesSoloSeActivanCuandoElServidorLoPermite()
    {
        var e = new Escenario();
        await e.Empezar();
        var enTurno = e.EnTurno;
        var rival = e.Otro(enTurno);
        var vistaTurno = Dto(await e.Ver(enTurno));
        var vistaRival = Dto(await e.Ver(rival));
        var heroe = vistaTurno.MiMano[0];

        Assert.That(Logica.PuedeColocar(vistaTurno, heroe), Is.True);
        Assert.That(Logica.PuedeColocar(vistaTurno, vistaRival.MiMano[0]), Is.False, "no es de tu mano");
        Assert.That(Logica.PuedeColocar(vistaRival, vistaRival.MiMano[0]), Is.False, "no es tu turno");
        Assert.That(Logica.PuedePasar(vistaTurno), Is.True);
        Assert.That(Logica.PuedePasar(vistaRival), Is.False);
        Assert.That(Logica.PuedeElegirHabilidad(vistaTurno, heroe), Is.False, "fase de colocar");
    }

    [Test]
    public async Task ConUnaPreguntaPendiente_NoSePuedeColocarNiElegirNiPasar()
    {
        var e = new Escenario();
        await e.LlevarAHabilidades();
        var jugador = e.EnTurno;
        var campo = (await e.Ver(jugador)).MiCampo.First(p => p.HabilidadesElegibles.Count > 0);
        Assert.That(Logica.PuedeElegirHabilidad(Dto(await e.Ver(jugador)), campo.HeroeId), Is.True);

        await e.Elegir(jugador, campo.HeroeId, campo.HabilidadesElegibles[0]);
        var dto = Dto(await e.Ver(jugador));

        Assert.That(dto.Pregunta, Is.Not.Null);
        Assert.That(Logica.PuedeElegirHabilidad(dto, campo.HeroeId), Is.False);
        Assert.That(Logica.PuedePasar(dto), Is.False);
    }

    // ---------------------------------------------------------------- textos

    [Test]
    public async Task ElTextoDelTurno_DiceLoQueTocaEnCadaMomento()
    {
        var e = new Escenario();
        var creada = Dto(await e.Servicio.CreateMatchAsync(Escenario.A, "r", "F1", e.Ahora));
        Assert.That(Logica.TextoTurno(creada), Does.Contain("Esperando").And.Contain(creada.Codigo));

        e = new Escenario();
        await e.Empezar();
        var enTurno = Dto(await e.Ver(e.EnTurno));
        var rival = Dto(await e.Ver(e.Otro(e.EnTurno)));
        Assert.That(Logica.TextoTurno(enTurno), Does.Contain("tu turno").And.Contain("UNA carta"));
        Assert.That(Logica.TextoTurno(rival), Does.Contain("rival"));

        var ganador = e.EnTurno;                  // al terminar, el servidor deja de tener a nadie "en turno"
        await e.Abandonar(e.Otro(ganador));
        var gana = Dto(await e.Ver(ganador));
        Assert.That(Logica.TextoTurno(gana), Does.Contain("GANASTE"));
    }

    [Test]
    public void SegundosRestantes_UsaElPlazoDelServidor_YNuncaSaleNegativo()
    {
        var v = new Cliente.VistaPartidaDto { DeadlineUtc = Reglas.Iso(Mesa.Hora.AddSeconds(30)) };
        Assert.That(Logica.SegundosRestantes(v, Mesa.Hora.AddSeconds(10)), Is.EqualTo(20));
        Assert.That(Logica.SegundosRestantes(v, Mesa.Hora.AddSeconds(99)), Is.EqualTo(0));
        Assert.That(Logica.SegundosRestantes(new Cliente.VistaPartidaDto(), Mesa.Hora), Is.EqualTo(0));
    }
}
