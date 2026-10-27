using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuizzerMatch;
using Cliente = QuizzerAttack.Partida;

namespace QuizzerMatch.Tests;

/// <summary>
/// El estado no se guarda: se deduce reproduciendo la lista de jugadas (como MatchStateStore.Replay en TTTXO).
/// </summary>
[TestFixture]
public class ReplayTests
{
    /// <summary>La vista sin lo que cambia entre llamadas (el resultado y la hora del servidor).</summary>
    private static string Normalizada(VistaPartida v)
    {
        var j = JObject.FromObject(v);
        j["Resultado"] = ""; j["Mensaje"] = ""; j["ServerTimeUtc"] = "";
        return j.ToString(Formatting.None);
    }

    [Test]
    public async Task LaVistaTrasJugar_EsLaMismaQueTrasReproducirLoGuardado()
    {
        // La vista que devuelve PlayMove sale del estado "vivo"; GetMatchState la construye reproduciendo el registro.
        // Si el motor no fuera determinista, aquí aparecería la diferencia.
        var e = new Escenario(semilla: 5);
        await e.LlevarAHabilidades();
        for (var paso = 0; paso < 40 && e.Estado.Estado == EstadoValores.EnJuego; paso++)
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
            Assert.That(Normalizada(await e.Ver(jugador)), Is.EqualTo(Normalizada(r)), $"paso {paso}");
        }
    }

    [Test]
    public async Task ReproducirDosVeces_DaExactamenteElMismoEstado()
    {
        var e = new Escenario(semilla: 9);
        await e.LlevarAHabilidades();
        var a = JsonConvert.SerializeObject(Motor.Reproducir(e.Registro));
        var b = JsonConvert.SerializeObject(Motor.Reproducir(e.Registro));
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public async Task ElPlazoVencido_SeAnotaComoJugadaDelServidor_YElReplayLoReproduce()
    {
        var e = new Escenario();
        await e.Empezar();
        var enTurno = e.EnTurno;
        e.Ahora = e.Ahora.AddSeconds(Config.SegundosPorTurno + 1);

        await e.Ver(e.Otro(enTurno));

        var plazo = e.Registro.Jugadas.Last();
        Assert.That(plazo.Tipo, Is.EqualTo(TipoJugada.Plazo));
        Assert.That(plazo.JugadorId, Is.Empty, "no lo hizo ningún jugador");
        Assert.That(e.EnTurno, Is.EqualTo(e.Otro(enTurno)), "el estado deducido ya lo refleja");
        // Consultar otra vez no anota otro plazo: el nuevo turno acaba de empezar.
        var n = e.Registro.Jugadas.Count;
        await e.Ver(e.Otro(enTurno));
        Assert.That(e.Registro.Jugadas, Has.Count.EqualTo(n));
    }

    [Test]
    public async Task ElClienteNoPuedeEnviarJugadasDelServidor()
    {
        var e = new Escenario();
        await e.Empezar();
        var jugador = e.EnTurno;
        var n = e.Registro.Jugadas.Count;

        foreach (var tipo in new[] { TipoJugada.Plazo, TipoJugada.Unirse, "inventada", "" })
        {
            var r = await e.Mover(jugador, Jugada.Propuesta(tipo));
            Assert.That(r.Resultado, Is.EqualTo(Resultado.Invalido), $"tipo '{tipo}'");
        }
        Assert.That(e.Registro.Jugadas, Has.Count.EqualTo(n));
    }

    [Test]
    public async Task QuienJuegaYLaHoraLosPoneElServidor_NoLaPropuesta()
    {
        var e = new Escenario();
        await e.Empezar();
        var jugador = e.EnTurno;
        var falsa = new Jugada
        {
            Tipo = TipoJugada.Colocar,
            HeroeId = e.Estado.Jugadores[jugador].Mano[0],
            JugadorId = e.Otro(jugador),           // intenta suplantar al rival
            HoraUtc = "2000-01-01T00:00:00.0000000Z",
            RequestId = "otro-id",
        };

        var r = await e.Servicio.PlayMoveAsync(jugador, e.Codigo, "mi-id", e.Estado.NumeroTurno, falsa, e.Ahora);

        Assert.That(r.Resultado, Is.EqualTo(Resultado.Aplicado));
        var anotada = e.Registro.Jugadas.Last();
        Assert.That(anotada.JugadorId, Is.EqualTo(jugador));
        Assert.That(anotada.RequestId, Is.EqualTo("mi-id"));
        Assert.That(anotada.HoraUtc, Is.EqualTo(Reglas.Iso(e.Ahora)));
    }

    [Test]
    public async Task UnRegistroQueYaNoSePuedeReproducir_LanzaEnVezDeInventarseElEstado()
    {
        var e = new Escenario();
        await e.Empezar();
        var registro = e.Registro;
        // Una jugada imposible: coloca el rival, que no tiene el turno.
        var fuera = e.Otro(e.EnTurno);
        registro.Jugadas.Add(new Jugada
        {
            Tipo = TipoJugada.Colocar, JugadorId = fuera, RequestId = "x",
            HoraUtc = Reglas.Iso(e.Ahora), HeroeId = e.Estado.Jugadores[fuera].Mano[0],
        });

        Assert.Throws<InvalidOperationException>(() => Motor.Reproducir(registro));
    }

    [Test]
    public async Task UnaPartidaDeOtraVersionDelProtocolo_SeRechazaConMensajeClaro()
    {
        var e = new Escenario();
        await e.Empezar();
        var registro = e.Registro;
        registro.Protocolo = GameProtocol.Version + 1;
        e.Repo.Poner(registro);

        var ex = Assert.ThrowsAsync<PartidaException>(() => e.Ver(Escenario.A));
        Assert.That(ex!.Message, Does.Contain("versión anterior"));
    }
}

/// <summary>
/// El contrato entre las dos mitades. El cliente de Unity tiene su propia copia de las vistas y de las
/// constantes (PartidaDto.cs, que esta prueba compila): si divergen, falla aquí en vez de en producción.
/// </summary>
[TestFixture]
public class ContratoTests
{
    private static readonly Dictionary<Type, Type> Espejos = new()
    {
        [typeof(VistaPartida)] = typeof(Cliente.VistaPartidaDto),
        [typeof(PeleadorVista)] = typeof(Cliente.PeleadorDto),
        [typeof(PreguntaVista)] = typeof(Cliente.PreguntaDto),
        [typeof(HabilidadVista)] = typeof(Cliente.HabilidadDto),
        [typeof(BuffVista)] = typeof(Cliente.BuffDto),
    };

    /// <summary>El nombre del tipo, llevando los tipos del servidor a su espejo del cliente (también dentro de List&lt;T&gt;).</summary>
    private static string Nombre(Type t, bool servidor)
    {
        if (t.IsGenericType)
            return t.Name.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(a => Nombre(a, servidor))) + ">";
        return servidor && Espejos.TryGetValue(t, out var cliente) ? cliente.Name : t.Name;
    }

    private static IEnumerable<string> Forma(Type t, bool servidor) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => $"{p.Name}:{Nombre(p.PropertyType, servidor)}").OrderBy(x => x, StringComparer.Ordinal);

    [Test]
    public void LasVistasDelClienteSonEspejoDeLasDelServidor()
    {
        foreach (var (servidor, cliente) in Espejos)
            Assert.That(Forma(cliente, false), Is.EqualTo(Forma(servidor, true)),
                $"{servidor.Name} y {cliente.Name} ya no tienen los mismos campos");
    }

    [Test]
    public void LaVersionDelProtocolo_EsLaMismaEnClienteYServidor()
    {
        Assert.That(Cliente.GameProtocol.Version, Is.EqualTo(GameProtocol.Version),
            "súbela en las dos a la vez (Core/GameProtocol.cs y Assets/Scripts/Online/Partida/PartidaDto.cs)");
    }

    [Test]
    public async Task LaVistaLlevaLaVersionDelServidor()
    {
        var e = new Escenario();
        var vista = await e.Empezar();
        Assert.That(vista.Protocolo, Is.EqualTo(GameProtocol.Version));
    }

    private static Dictionary<string, string> Constantes(Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    [Test]
    public void LasConstantesDelClienteCoincidenConLasDelServidor()
    {
        Assert.That(Constantes(typeof(Cliente.ResultadoPartida)), Is.EqualTo(Constantes(typeof(Resultado))));
        Assert.That(Constantes(typeof(Cliente.EstadoPartidaValores)), Is.EqualTo(Constantes(typeof(EstadoValores))));
        Assert.That(Constantes(typeof(Cliente.FasePartidaValores)), Is.EqualTo(Constantes(typeof(FaseValores))));
        Assert.That(Constantes(typeof(Cliente.TipoPush)), Is.EqualTo(Constantes(typeof(TipoPush))));
    }

    [Test]
    public void ElPush_LlevaSoloTipoYPartida_Nunca_Estado()
    {
        var cuerpo = JObject.Parse(NotificadorPush.CrearMensaje("ABC234"));

        Assert.That(cuerpo.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "type", "matchId" }));
        Assert.That((string?)cuerpo["type"], Is.EqualTo(TipoPush.MatchUpdated));
        // El cliente lo lee con su DTO: los nombres deben emparejar.
        var dto = JsonConvert.DeserializeObject<Cliente.AvisoPushDto>(cuerpo.ToString())!;
        Assert.That((dto.Type, dto.MatchId), Is.EqualTo((TipoPush.MatchUpdated, "ABC234")));
    }

    [Test]
    public async Task TodasLasFuncionesDeLaPartida_AvisanAlRivalYNuncaAlQueJuega()
    {
        var e = new Escenario(semilla: 3);
        await e.Empezar();
        var jugador = e.EnTurno;
        var antes = e.Avisos.Avisos.Count;

        await e.Colocar(jugador, e.Estado.Jugadores[jugador].Mano[0]);

        Assert.That(e.Avisos.Avisos.Skip(antes), Is.EqualTo(new[] { new Aviso(e.Otro(jugador), e.Codigo) }));
    }
}
