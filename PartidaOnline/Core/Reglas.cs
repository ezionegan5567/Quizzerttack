using System.Globalization;

namespace QuizzerMatch;

/// <summary>
/// Valores ajustables. Donde el GDD y el código de Unity discrepan se ha tomado el GDD y
/// se anota el valor del código para que el equipo decida cuál es el correcto.
/// </summary>
public static class Config
{
    public const int SegundosPorTurno = 45;     // GDD: 45 s. Código actual (Temporizador): 30 s.
    public const int SegundosQuiz = 20;
    public const int BonoAciertoPct = 25;       // GDD: +25 %. Código actual (QuizHabilidades): +10 %.
    public const int PenalFalloPct = 25;        // GDD: -25 %. Código actual: -10 %.
    public const int BonoPorRondaPct = 10;      // GDD: +10 % de ataque por ronda. Aún no existe en Unity.
    public const int MaxEnCampo = 3;
    public const int MaxTimeoutsSeguidos = 3;   // 3 plazos vencidos seguidos = abandono
    public const int MaxRondas = 30;            // red de seguridad contra partidas eternas
}

/// <summary>
/// Las reglas de la partida, en C# puro: sin Unity, sin red, sin Cloud Save. Toda función
/// recibe el estado y la hora (nunca llama a DateTime.UtcNow), así que se prueban con
/// tests normales y producen siempre el mismo resultado.
///
/// Es la autoridad: el cliente sólo pide; aquí se valida turno, fase, propiedad de las
/// cartas, límites y tiempo.
/// </summary>
public static class Reglas
{
    /// <summary>
    /// Efectos que el motor sabe ejecutar. Una habilidad cuyo efecto principal no esté aquí
    /// existe en el catálogo pero no se puede elegir todavía (se rechaza con un mensaje).
    /// Para añadir un efecto: implementarlo en AplicarEfecto, añadirlo aquí y escribir su test.
    /// </summary>
    public static readonly HashSet<Efecto> EfectosSoportados = new()
    {
        Efecto.Dano, Efecto.DanoTodos, Efecto.DanoDos,
        Efecto.Curar, Efecto.CurarZona,
        Efecto.BuffAtk, Efecto.BuffAtkZona, Efecto.BuffDef, Efecto.BuffDefZona,
        Efecto.Intimidar,
    };

    public static bool Disponible(Habilidad h) => EfectosSoportados.Contains(h.Efecto);

    // ---------------------------------------------------------------- alta de la partida

    public static bool FaccionValida(string? faccion) =>
        string.Equals(faccion, "Random", StringComparison.OrdinalIgnoreCase)
        || Catalogo.Facciones.Any(f => string.Equals(f, faccion, StringComparison.OrdinalIgnoreCase));

    /// <summary>Devuelve null si la facción no es válida. "Random" la sortea con la semilla del servidor.</summary>
    public static string? ResolverFaccion(EstadoPartida s, string faccion)
    {
        if (string.Equals(faccion, "Random", StringComparison.OrdinalIgnoreCase))
            return Catalogo.Facciones[Siguiente(s, Catalogo.Facciones.Length)];
        return Catalogo.Facciones.FirstOrDefault(f => string.Equals(f, faccion, StringComparison.OrdinalIgnoreCase));
    }

    public static EstadoPartida Crear(string codigo, string anfitrionId, string requestId, int semilla, DateTime ahora)
    {
        return new EstadoPartida
        {
            Codigo = codigo,
            AnfitrionId = anfitrionId,
            CreadaPorRequestId = requestId,
            Semilla = semilla,
            CreadaUtc = Iso(ahora),
            ActualizadaUtc = Iso(ahora),
        };
    }

    public static void AsignarAnfitrion(EstadoPartida s, string faccion) =>
        s.Jugadores[s.AnfitrionId] = NuevoJugador(s.AnfitrionId, faccion);

    /// <summary>La jugada "unirse": valida y, si procede, entra el segundo jugador.</summary>
    public static Resolucion Unirse(EstadoPartida s, string invitadoId, string faccion, DateTime ahora)
    {
        if (s.Estado != EstadoValores.EsperandoRival || s.InvitadoId.Length > 0)
            return Resolucion.Rechazo(Resultado.Invalido, "Esta partida ya no admite jugadores.");
        if (invitadoId.Length == 0 || invitadoId == s.AnfitrionId)
            return Resolucion.Rechazo(Resultado.Invalido, "No puedes unirte a tu propia partida.");
        var facc = ResolverFaccion(s, faccion);
        if (facc == null)
            return Resolucion.Rechazo(Resultado.Invalido, "Facción no válida. Usa F1, F2, F3, F4 o Random.");
        Unir(s, invitadoId, facc, ahora);
        return Resolucion.Ok("Partida iniciada.");
    }

    /// <summary>Entra el segundo jugador; el servidor lanza la moneda y abre la primera ronda.</summary>
    public static void Unir(EstadoPartida s, string invitadoId, string faccion, DateTime ahora)
    {
        s.InvitadoId = invitadoId;
        s.Jugadores[invitadoId] = NuevoJugador(invitadoId, faccion);
        s.JugadorInicialId = Siguiente(s, 2) == 0 ? s.AnfitrionId : s.InvitadoId;
        s.Estado = EstadoValores.EnJuego;
        s.Fase = FaseValores.Colocar;
        s.Paso = 0;
        s.Ronda = 1;
        s.JugadorEnTurnoId = s.JugadorInicialId;
        s.NumeroTurno = 1;
        FijarPlazo(s, ahora);
        Tocar(s, ahora);
    }

    private static JugadorEstado NuevoJugador(string id, string faccion) => new()
    {
        Id = id,
        Faccion = faccion,
        Mano = Catalogo.DeFaccion(faccion).Select(h => h.Id).ToList(),
    };

    // ---------------------------------------------------------------- jugadas

    public static Resolucion Colocar(EstadoPartida s, string jugadorId, string heroeId, int casilla, DateTime ahora)
    {
        var error = ValidarTurno(s, jugadorId, FaseValores.Colocar);
        if (error != null) return error;

        var j = s.Jugadores[jugadorId];
        if (!j.Mano.Contains(heroeId))
            return Resolucion.Rechazo(Resultado.Invalido, "Ese héroe no está en tu mano.");
        if (j.Campo.Count >= Config.MaxEnCampo)
            return Resolucion.Rechazo(Resultado.Invalido, $"Ya tienes {Config.MaxEnCampo} héroes en el campo.");
        if (casilla < -1 || casilla >= Config.MaxEnCampo)
            return Resolucion.Rechazo(Resultado.Invalido, "Casilla fuera de rango (0-2, o -1 para la primera libre).");

        if (casilla == -1)
            casilla = Enumerable.Range(0, Config.MaxEnCampo).First(c => j.Campo.All(p => p.Casilla != c));
        else if (j.Campo.Any(p => p.Casilla == casilla))
            return Resolucion.Rechazo(Resultado.Invalido, "Esa casilla ya está ocupada.");

        var def = Catalogo.Heroe(heroeId);
        j.Mano.Remove(heroeId);
        j.Campo.Add(new PeleadorEstado { HeroeId = heroeId, Casilla = casilla, Vida = def.Vida, VidaMax = def.Vida });
        j.TimeoutsSeguidos = 0;

        Avanzar(s, ahora);
        return Resolucion.Ok($"{def.Nombre} entra en la casilla {casilla}.");
    }

    public static Resolucion ElegirHabilidad(EstadoPartida s, string jugadorId, string peleadorId, int indice, DateTime ahora)
    {
        var error = ValidarTurno(s, jugadorId, FaseValores.Habilidades);
        if (error != null) return error;
        if (s.Pregunta != null)
            return Resolucion.Rechazo(Resultado.Invalido, "Primero responde la pregunta pendiente.");

        var p = s.Jugadores[jugadorId].Campo.FirstOrDefault(x => x.HeroeId == peleadorId && x.EstaVivo);
        if (p == null)
            return Resolucion.Rechazo(Resultado.Invalido, "Ese peleador no es tuyo, no está en el campo o ya cayó.");
        if (p.HabilidadPreparada >= 0)
            return Resolucion.Rechazo(Resultado.Invalido, "Ese peleador ya tiene una habilidad preparada.");

        var def = Catalogo.Heroe(p.HeroeId);
        if (indice < 0 || indice >= def.Habilidades.Length)
            return Resolucion.Rechazo(Resultado.Invalido, "Índice de habilidad fuera de rango.");
        var hab = def.Habilidades[indice];
        if (!Disponible(hab))
            return Resolucion.Rechazo(Resultado.Invalido, $"'{hab.Nombre}' ({hab.Efecto}) aún no está implementada en el servidor.");
        if (p.Usadas.Contains(indice))
            return Resolucion.Rechazo(Resultado.Invalido, "Esa habilidad ya se usó; agota el resto antes de repetirla.");

        var pregunta = BancoPreguntas.Obtener(Siguiente(s, BancoPreguntas.Cantidad));
        var vence = Min(ahora.AddSeconds(Config.SegundosQuiz), Parse(s.DeadlineUtc));
        s.Pregunta = new PreguntaPendiente
        {
            JugadorId = jugadorId,
            PeleadorId = peleadorId,
            IndiceHabilidad = indice,
            PreguntaId = pregunta.Id,
            VenceUtc = Iso(vence),
        };
        s.Jugadores[jugadorId].TimeoutsSeguidos = 0;
        s.NumeroTurno++;
        Tocar(s, ahora);
        return Resolucion.Ok("Responde la pregunta para fijar la habilidad.");
    }

    public static Resolucion ResponderQuiz(EstadoPartida s, string jugadorId, int preguntaId, int opcion, DateTime ahora)
    {
        var error = ValidarTurno(s, jugadorId, FaseValores.Habilidades);
        if (error != null) return error;
        var pendiente = s.Pregunta;
        if (pendiente == null || pendiente.JugadorId != jugadorId)
            return Resolucion.Rechazo(Resultado.Invalido, "No tienes ninguna pregunta pendiente.");
        if (pendiente.PreguntaId != preguntaId)
            return Resolucion.Rechazo(Resultado.Invalido, "Esa no es tu pregunta pendiente.");

        var pregunta = BancoPreguntas.Obtener(pendiente.PreguntaId);
        if (opcion < 0 || opcion >= pregunta.Opciones.Length)
            return Resolucion.Rechazo(Resultado.Invalido, "Opción fuera de rango.");

        var aTiempo = ahora <= Parse(pendiente.VenceUtc);
        var acierto = aTiempo && opcion == pregunta.Correcta;
        s.Jugadores[jugadorId].TimeoutsSeguidos = 0;
        FijarRespuesta(s, acierto, ahora, avanzarSiTermina: true);

        var mensaje = acierto ? $"¡Correcto! La habilidad gana +{Config.BonoAciertoPct} %."
            : aTiempo ? $"Incorrecto: la habilidad pierde {Config.PenalFalloPct} %."
            : $"Tiempo agotado: la habilidad pierde {Config.PenalFalloPct} %.";
        return Resolucion.Ok(mensaje);
    }

    public static Resolucion PasarTurno(EstadoPartida s, string jugadorId, DateTime ahora)
    {
        if (s.Estado != EstadoValores.EnJuego)
            return Resolucion.Rechazo(Resultado.Terminada, "La partida no está en juego.");
        if (s.JugadorEnTurnoId != jugadorId)
            return Resolucion.Rechazo(Resultado.NoEsTuTurno, "No es tu turno.");
        if (s.Pregunta != null)
            return Resolucion.Rechazo(Resultado.Invalido, "Responde la pregunta pendiente antes de pasar.");

        s.Jugadores[jugadorId].TimeoutsSeguidos = 0;
        Avanzar(s, ahora);
        return Resolucion.Ok("Turno pasado.");
    }

    public static Resolucion Abandonar(EstadoPartida s, string jugadorId, DateTime ahora)
    {
        if (s.Estado == EstadoValores.Terminada)
            return Resolucion.Rechazo(Resultado.Terminada, "La partida ya terminó.");

        if (s.Estado == EstadoValores.EsperandoRival)
            Terminar(s, "", "cancelada", ahora);
        else
            Terminar(s, s.RivalDe(jugadorId), "abandono", ahora);
        return Resolucion.Ok("Has abandonado la partida.");
    }

    /// <summary>
    /// Aplica el plazo vencido, si lo hay. Cloud Code no tiene temporizadores: esto se llama
    /// al principio de cada petición, así que el reloj es siempre el del servidor.
    /// Devuelve true si cambió el estado.
    /// </summary>
    public static bool AplicarPlazo(EstadoPartida s, DateTime ahora)
    {
        if (s.Estado != EstadoValores.EnJuego || s.DeadlineUtc.Length == 0 || ahora <= Parse(s.DeadlineUtc))
            return false;

        var j = s.Jugadores[s.JugadorEnTurnoId];
        j.TimeoutsSeguidos++;
        if (j.TimeoutsSeguidos >= Config.MaxTimeoutsSeguidos)
        {
            Terminar(s, s.RivalDe(j.Id), "timeouts", ahora);
            return true;
        }

        if (s.Pregunta != null)
            FijarRespuesta(s, acierto: false, ahora, avanzarSiTermina: false);
        Avanzar(s, ahora);
        return true;
    }

    // ---------------------------------------------------------------- validaciones comunes

    private static Resolucion? ValidarTurno(EstadoPartida s, string jugadorId, string fase)
    {
        if (s.Estado != EstadoValores.EnJuego)
            return Resolucion.Rechazo(Resultado.Terminada, "La partida no está en juego.");
        if (s.JugadorEnTurnoId != jugadorId)
            return Resolucion.Rechazo(Resultado.NoEsTuTurno, "No es tu turno.");
        if (s.Fase != fase)
            return Resolucion.Rechazo(Resultado.FaseIncorrecta, $"Ahora toca la fase '{s.Fase}', no '{fase}'.");
        return null;
    }

    // ---------------------------------------------------------------- fases

    private static void FijarRespuesta(EstadoPartida s, bool acierto, DateTime ahora, bool avanzarSiTermina)
    {
        var pendiente = s.Pregunta!;
        var j = s.Jugadores[pendiente.JugadorId];
        var p = j.Campo.First(x => x.HeroeId == pendiente.PeleadorId);
        p.HabilidadPreparada = pendiente.IndiceHabilidad;
        p.MultiplicadorPreparado = 1.0 + (acierto ? Config.BonoAciertoPct : -Config.PenalFalloPct) / 100.0;
        s.Pregunta = null;

        if (avanzarSiTermina && !QuedanPorElegir(j))
        {
            Avanzar(s, ahora);
        }
        else
        {
            s.NumeroTurno++;
            Tocar(s, ahora);
        }
    }

    /// <summary>Pasa al siguiente paso de la ronda, saltando al jugador que no pueda hacer nada.</summary>
    private static void Avanzar(EstadoPartida s, DateTime ahora)
    {
        s.Pregunta = null;
        SiguientePaso(s, ahora);
        // Como mucho una ronda completa (4 pasos) más la pelea.
        for (var i = 0; i < 6 && s.Estado == EstadoValores.EnJuego && !PuedeActuar(s, s.JugadorEnTurnoId); i++)
            SiguientePaso(s, ahora);
        if (s.Estado == EstadoValores.EnJuego) FijarPlazo(s, ahora);
        s.NumeroTurno++;
        Tocar(s, ahora);
    }

    private static void SiguientePaso(EstadoPartida s, DateTime ahora)
    {
        var inicial = s.JugadorInicialId;
        var segundo = s.RivalDe(inicial);

        if (s.Fase == FaseValores.Colocar && s.Paso == 0)
        {
            s.Paso = 1;
            s.JugadorEnTurnoId = segundo;
        }
        else if (s.Fase == FaseValores.Colocar)
        {
            s.Fase = FaseValores.Habilidades;
            s.Paso = 0;
            s.JugadorEnTurnoId = inicial;
        }
        else if (s.Paso == 0)
        {
            s.Paso = 1;
            s.JugadorEnTurnoId = segundo;
        }
        else
        {
            ResolverPelea(s, ahora);
            if (s.Estado != EstadoValores.EnJuego) return;
            s.Ronda++;
            s.Fase = FaseValores.Colocar;
            s.Paso = 0;
            s.JugadorEnTurnoId = inicial;
        }
    }

    private static bool PuedeActuar(EstadoPartida s, string jugadorId)
    {
        var j = s.Jugadores[jugadorId];
        return s.Fase == FaseValores.Colocar
            ? j.Mano.Count > 0 && j.Campo.Count < Config.MaxEnCampo
            : QuedanPorElegir(j);
    }

    private static bool QuedanPorElegir(JugadorEstado j) =>
        j.Campo.Any(p => p.EstaVivo && p.HabilidadPreparada < 0 && Elegibles(p).Any());

    public static IEnumerable<int> Elegibles(PeleadorEstado p)
    {
        var habs = Catalogo.Heroe(p.HeroeId).Habilidades;
        for (var i = 0; i < habs.Length; i++)
            if (Disponible(habs[i]) && !p.Usadas.Contains(i)) yield return i;
    }

    // ---------------------------------------------------------------- pelea

    private static void ResolverPelea(EstadoPartida s, DateTime ahora)
    {
        var log = new List<string>();
        var orden = new[] { s.AnfitrionId, s.InvitadoId };   // como GestorTurnos: primero P1, luego P2, por casilla

        foreach (var id in orden)
        {
            foreach (var p in s.Jugadores[id].Campo.OrderBy(c => c.Casilla).ToList())
            {
                if (!p.EstaVivo || p.HabilidadPreparada < 0) continue;

                var def = Catalogo.Heroe(p.HeroeId);
                var hab = def.Habilidades[p.HabilidadPreparada];
                var factor = Math.Max(0.1, 1.0 + SumaBuffs(p, "atk") / 100.0)
                           * (1.0 + Config.BonoPorRondaPct * (s.Ronda - 1) / 100.0)
                           * p.MultiplicadorPreparado;

                log.Add($"{Etiqueta(s, id)}: {def.Nombre} usa {hab.Nombre} (x{factor.ToString("0.00", CultureInfo.InvariantCulture)})");
                AplicarEfecto(s, id, p, hab.Efecto, hab.Objetivo, hab.Valor, hab.Duracion, factor, log);
                if (EfectosSoportados.Contains(hab.Efecto2))
                    AplicarEfecto(s, id, p, hab.Efecto2, hab.Objetivo2, hab.Valor2, hab.Duracion2, factor, log);

                p.Usadas.Add(p.HabilidadPreparada);
                if (!Elegibles(p).Any()) p.Usadas.Clear();   // GDD: se reinicia al agotar el repertorio
            }
        }

        foreach (var j in s.Jugadores.Values)
        {
            foreach (var p in j.Campo)
            {
                p.HabilidadPreparada = -1;
                p.MultiplicadorPreparado = 1.0;
                foreach (var b in p.Buffs) b.Rondas--;
                p.Buffs.RemoveAll(b => b.Rondas <= 0);
                if (!p.EstaVivo) log.Add($"{Etiqueta(s, j.Id)}: {Catalogo.Heroe(p.HeroeId).Nombre} cae.");
            }
            j.Campo.RemoveAll(p => !p.EstaVivo);
        }
        s.RegistroPelea = log;

        var sigueAnf = SigueEnPie(s.Jugadores[s.AnfitrionId]);
        var sigueInv = SigueEnPie(s.Jugadores[s.InvitadoId]);
        if (!sigueAnf && !sigueInv) Terminar(s, "", "empate", ahora);
        else if (!sigueAnf) Terminar(s, s.InvitadoId, "sin_peleadores", ahora);
        else if (!sigueInv) Terminar(s, s.AnfitrionId, "sin_peleadores", ahora);
        else if (s.Ronda >= Config.MaxRondas) Terminar(s, "", "limite_de_rondas", ahora);
    }

    // Como en GestorTurnos.VerificarFin: sigue quien tiene peleadores vivos o cartas sin usar.
    private static bool SigueEnPie(JugadorEstado j) => j.Campo.Any(p => p.EstaVivo) || j.Mano.Count > 0;

    private static void AplicarEfecto(
        EstadoPartida s, string duenoId, PeleadorEstado lanzador, Efecto efecto, Objetivo objetivo,
        int valor, int duracion, double factor, List<string> log)
    {
        var aliados = s.Jugadores[duenoId].Campo.Where(p => p.EstaVivo).ToList();
        var enemigos = s.Jugadores[s.RivalDe(duenoId)].Campo.Where(p => p.EstaVivo).OrderBy(p => p.Casilla).ToList();
        var escalado = (int)Math.Round(valor * lanzador.MultiplicadorPreparado);
        var rondas = Math.Max(1, duracion);

        switch (efecto)
        {
            case Efecto.Dano:
                Golpear(new[] { EnemigoEnCarril(enemigos, lanzador.Casilla) }, valor, factor, log);
                break;
            case Efecto.DanoDos:
                var primero = EnemigoEnCarril(enemigos, lanzador.Casilla);
                var objetivos = new List<PeleadorEstado?> { primero };
                objetivos.AddRange(enemigos.Where(e => e != primero).Take(1));
                Golpear(objetivos, valor, factor, log);
                break;
            case Efecto.DanoTodos:
                Golpear(enemigos, valor, factor, log);
                break;
            case Efecto.Curar:
                Curar(ElegirAliado(aliados, lanzador, objetivo), escalado, log);
                break;
            case Efecto.CurarZona:
                foreach (var a in aliados) Curar(a, escalado, log);
                break;
            case Efecto.BuffAtk:
                PonerBuff(ElegirAliado(aliados, lanzador, objetivo), "atk", escalado, rondas, log);
                break;
            case Efecto.BuffAtkZona:
                foreach (var a in aliados) PonerBuff(a, "atk", escalado, rondas, log);
                break;
            case Efecto.BuffDef:
                PonerBuff(ElegirAliado(aliados, lanzador, objetivo), "def", escalado, rondas, log);
                break;
            case Efecto.BuffDefZona:
                foreach (var a in aliados) PonerBuff(a, "def", escalado, rondas, log);
                break;
            case Efecto.Intimidar:
                // Un debuff es un buff negativo sobre el enemigo del carril.
                PonerBuff(EnemigoEnCarril(enemigos, lanzador.Casilla), "atk", -escalado, rondas, log);
                break;
        }
    }

    // Igual que Peleador.EnemigoEnCarril sin la provocación: mismo carril, y si no hay, el primero vivo.
    private static PeleadorEstado? EnemigoEnCarril(List<PeleadorEstado> enemigos, int casilla) =>
        enemigos.FirstOrDefault(e => e.Casilla == casilla) ?? enemigos.FirstOrDefault();

    private static PeleadorEstado? ElegirAliado(List<PeleadorEstado> aliados, PeleadorEstado lanzador, Objetivo objetivo) =>
        objetivo == Objetivo.Aliado
            ? aliados.OrderBy(a => (double)a.Vida / a.VidaMax).FirstOrDefault()
            : lanzador;

    private static void Golpear(IEnumerable<PeleadorEstado?> objetivos, int valor, double factor, List<string> log)
    {
        foreach (var o in objetivos.Where(x => x != null && x.EstaVivo))
        {
            var defensa = Math.Clamp(SumaBuffs(o!, "def"), -50, 75);
            var dano = Math.Max(1, (int)Math.Round(valor * factor * (1.0 - defensa / 100.0)));
            o!.Vida = Math.Max(0, o.Vida - dano);
            log.Add($"  {Catalogo.Heroe(o.HeroeId).Nombre} recibe {dano} (vida {o.Vida}/{o.VidaMax})");
        }
    }

    private static void Curar(PeleadorEstado? objetivo, int cantidad, List<string> log)
    {
        if (objetivo == null || !objetivo.EstaVivo) return;
        objetivo.Vida = Math.Min(objetivo.VidaMax, objetivo.Vida + cantidad);
        log.Add($"  {Catalogo.Heroe(objetivo.HeroeId).Nombre} se cura {cantidad} (vida {objetivo.Vida}/{objetivo.VidaMax})");
    }

    private static void PonerBuff(PeleadorEstado? objetivo, string tipo, int valor, int rondas, List<string> log)
    {
        if (objetivo == null || !objetivo.EstaVivo) return;
        objetivo.Buffs.Add(new Buff { Tipo = tipo, Valor = valor, Rondas = rondas });
        log.Add($"  {Catalogo.Heroe(objetivo.HeroeId).Nombre}: {(tipo == "atk" ? "ataque" : "defensa")} {(valor >= 0 ? "+" : "")}{valor} % ({rondas} ronda/s)");
    }

    private static int SumaBuffs(PeleadorEstado p, string tipo) => p.Buffs.Where(b => b.Tipo == tipo).Sum(b => b.Valor);

    private static string Etiqueta(EstadoPartida s, string id) => id == s.AnfitrionId ? "Anfitrión" : "Invitado";

    // ---------------------------------------------------------------- utilidades

    private static void Terminar(EstadoPartida s, string ganadorId, string motivo, DateTime ahora)
    {
        s.Estado = EstadoValores.Terminada;
        s.Fase = "";
        s.GanadorId = ganadorId;
        s.MotivoFin = motivo;
        s.Pregunta = null;
        s.DeadlineUtc = "";
        s.JugadorEnTurnoId = "";
        Tocar(s, ahora);
    }

    private static void FijarPlazo(EstadoPartida s, DateTime ahora) =>
        s.DeadlineUtc = Iso(ahora.AddSeconds(Config.SegundosPorTurno));

    private static void Tocar(EstadoPartida s, DateTime ahora) => s.ActualizadaUtc = Iso(ahora);

    /// <summary>
    /// Aleatoriedad del servidor, reproducible: semilla secreta + contador. Con el mismo
    /// estado inicial y las mismas jugadas, la partida se repite igual (útil para depurar y auditar).
    /// El cliente nunca ve la semilla ni elige nada de esto.
    /// </summary>
    private static int Siguiente(EstadoPartida s, int max) =>
        new Random(unchecked(s.Semilla * 31 + s.RngContador++)).Next(max);

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    public static string Iso(DateTime t) => t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    public static DateTime Parse(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
