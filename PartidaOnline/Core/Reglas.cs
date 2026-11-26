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
    /// <summary>Todos los efectos activos que también ejecuta Peleador en la batalla local.</summary>
    public static readonly HashSet<Efecto> EfectosSoportados = new()
    {
        Efecto.Dano, Efecto.DanoTodos, Efecto.DanoDos,
        Efecto.Veneno, Efecto.Sangrado, Efecto.Quemadura, Efecto.Maldicion,
        Efecto.Curar, Efecto.CurarZona, Efecto.HotZona,
        Efecto.BuffAtk, Efecto.BuffAtkZona, Efecto.BuffDef, Efecto.BuffDefZona,
        Efecto.Velocidad, Efecto.VelocidadZona, Efecto.Punteria, Efecto.Lentitud,
        Efecto.Intimidar, Efecto.Ceguera, Efecto.Paralisis, Efecto.Incapacitar,
        Efecto.Invisible, Efecto.Provocar, Efecto.Inmune, Efecto.InmuneZona,
        Efecto.Impedir, Efecto.Proteger, Efecto.Purificar, Efecto.MarcaPolvora,
        Efecto.Reflejo, Efecto.Confusion,
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
        if (!PuedeActuar(p))
            return Resolucion.Rechazo(Resultado.Invalido, "Ese peleador está controlado y no puede actuar esta ronda.");

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
        j.Campo.Any(p => p.EstaVivo && PuedeActuar(p) && p.HabilidadPreparada < 0 && Elegibles(p).Any());

    public static IEnumerable<int> Elegibles(PeleadorEstado p)
    {
        if (!PuedeActuar(p)) yield break;
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
                if (!p.EstaVivo || p.HabilidadPreparada < 0 || !PuedeActuar(p)) continue;

                var def = Catalogo.Heroe(p.HeroeId);
                var hab = def.Habilidades[p.HabilidadPreparada];
                var factor = Math.Max(0.1, 1.0 + (SumaBuffs(p, Efecto.BuffAtk) - SumaBuffs(p, Efecto.Lentitud)) / 100.0)
                           * (1.0 + Config.BonoPorRondaPct * (s.Ronda - 1) / 100.0)
                           * p.MultiplicadorPreparado;

                log.Add($"{Etiqueta(s, id)}: {def.Nombre} usa {hab.Nombre} (x{factor.ToString("0.00", CultureInfo.InvariantCulture)})");
                int causado = AplicarEfecto(s, id, p, hab.Efecto, hab.Objetivo, hab.Valor, hab.Duracion, factor, hab.Fuego, log);
                if (EfectosSoportados.Contains(hab.Efecto2))
                    causado += AplicarEfecto(s, id, p, hab.Efecto2, hab.Objetivo2, hab.Valor2, hab.Duracion2, factor, hab.Fuego, log);

                var nombreHabilidad = hab.Nombre.ToLowerInvariant();
                if (def.Pasiva == Efecto.ContadorDevolver && p.StacksDevolver > 0 && nombreHabilidad.Contains("perfor"))
                {
                    var objetivo = SeleccionEnemigo(s.Jugadores[s.RivalDe(id)].Campo.Where(x => x.EstaVivo).ToList(), p.Casilla).FirstOrDefault();
                    causado += Golpear(s, id, p, new[] { objetivo }, (int)Math.Round(def.Ataque * 0.25 * p.StacksDevolver), 1.0, false, log);
                    p.StacksDevolver = 0;
                }
                if (def.Pasiva == Efecto.ContadorSangre && p.StacksSangre > 0 && nombreHabilidad.Contains("sangre"))
                {
                    var objetivo = SeleccionEnemigo(s.Jugadores[s.RivalDe(id)].Campo.Where(x => x.EstaVivo).ToList(), p.Casilla).FirstOrDefault();
                    causado += Golpear(s, id, p, new[] { objetivo }, p.StacksSangre * 5, 1.0, false, log);
                    p.StacksSangre = 0;
                }
                if (hab.Robo > 0 && causado > 0) Curar(p, (int)Math.Round(causado * hab.Robo), log);

                p.Usadas.Add(p.HabilidadPreparada);
                if (!Elegibles(p).Any()) p.Usadas.Clear();   // GDD: se reinicia al agotar el repertorio
            }
        }

        foreach (var j in s.Jugadores.Values)
        {
            foreach (var p in j.Campo)
            {
                TickEstados(s, j.Id, p, log);
                p.HabilidadPreparada = -1;
                p.MultiplicadorPreparado = 1.0;
                foreach (var b in p.Buffs) b.Rondas--;
                p.Buffs.RemoveAll(b => b.Rondas <= 0);
                if (p.ProtectorJugadorId.Length > 0)
                {
                    var protector = s.Jugadores.TryGetValue(p.ProtectorJugadorId, out var pj)
                        ? pj.Campo.FirstOrDefault(x => x.HeroeId == p.ProtectorHeroeId && x.EstaVivo) : null;
                    if (protector == null || !Tiene(p, Efecto.Proteger))
                    {
                        p.ProtectorJugadorId = "";
                        p.ProtectorHeroeId = "";
                    }
                }
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

    private static int AplicarEfecto(
        EstadoPartida s, string duenoId, PeleadorEstado lanzador, Efecto efecto, Objetivo objetivo,
        int valor, int duracion, double factor, bool fuego, List<string> log)
    {
        if (efecto == Efecto.Ninguno) return 0;
        var aliados = s.Jugadores[duenoId].Campo.Where(p => p.EstaVivo).ToList();
        var enemigos = s.Jugadores[s.RivalDe(duenoId)].Campo.Where(p => p.EstaVivo).OrderBy(p => p.Casilla).ToList();
        var escalado = (int)Math.Round(valor * lanzador.MultiplicadorPreparado, MidpointRounding.AwayFromZero);
        var rondas = duracion;
        var objetivos = Objetivos(s, duenoId, lanzador, objetivo, aliados, enemigos);
        var totalDano = 0;

        switch (efecto)
        {
            case Efecto.Dano:
                totalDano += Golpear(s, duenoId, lanzador, objetivos, valor, factor, fuego, log);
                break;
            case Efecto.DanoDos:
            {
                var dosObjetivos = objetivos.ToList();
                dosObjetivos.AddRange(enemigos.Where(e => !Tiene(e, Efecto.Invisible) && !dosObjetivos.Contains(e)).Take(1));
                totalDano += Golpear(s, duenoId, lanzador, dosObjetivos, valor, factor, fuego, log);
                break;
            }
            case Efecto.DanoTodos:
                totalDano += Golpear(s, duenoId, lanzador, enemigos.Where(e => !Tiene(e, Efecto.Invisible)), valor, factor, fuego, log);
                break;
            case Efecto.Veneno:
            case Efecto.Sangrado:
            case Efecto.Quemadura:
            case Efecto.Maldicion:
            case Efecto.Ceguera:
            case Efecto.Paralisis:
            case Efecto.Incapacitar:
            case Efecto.Invisible:
            case Efecto.Provocar:
            case Efecto.Inmune:
            case Efecto.Punteria:
            case Efecto.MarcaPolvora:
            case Efecto.Reflejo:
            case Efecto.Confusion:
            case Efecto.Lentitud:
            case Efecto.Velocidad:
            case Efecto.Impedir:
                foreach (var t in objetivos) AnadirEfecto(t, efecto, efecto == Efecto.Impedir ? 1 : escalado, rondas);
                break;
            case Efecto.HotZona:
                foreach (var t in aliados) AnadirEfecto(t, efecto, escalado, rondas);
                break;
            case Efecto.Curar:
                Curar(Primero(objetivos), escalado, log);
                break;
            case Efecto.CurarZona:
                foreach (var a in aliados) Curar(a, escalado, log);
                break;
            case Efecto.BuffAtk:
            case Efecto.BuffDef:
            case Efecto.Intimidar:
            {
                var tipoBuff = efecto == Efecto.BuffDef ? Efecto.BuffDef : Efecto.BuffAtk;
                var valBuff = efecto == Efecto.Intimidar ? -escalado : escalado;
                foreach (var t in objetivos) PonerBuff(t, tipoBuff, valBuff, rondas, log);
                break;
            }
            case Efecto.BuffAtkZona:
            {
                foreach (var a in aliados) PonerBuff(a, Efecto.BuffAtk, escalado, rondas, log);
                break;
            }
            case Efecto.BuffDefZona:
            {
                foreach (var a in aliados) PonerBuff(a, Efecto.BuffDef, escalado, rondas, log);
                break;
            }
            case Efecto.VelocidadZona:
                foreach (var a in aliados) AnadirEfecto(a, Efecto.Velocidad, escalado, rondas);
                break;
            case Efecto.InmuneZona:
                foreach (var a in aliados) AnadirEfecto(a, Efecto.Inmune, 0, rondas);
                break;
            case Efecto.Proteger:
                foreach (var t in objetivos.Where(t => t != lanzador))
                {
                    t.ProtectorJugadorId = duenoId;
                    t.ProtectorHeroeId = lanzador.HeroeId;
                    AnadirEfecto(t, Efecto.Proteger, 0, rondas);
                }
                break;
            case Efecto.Purificar:
                foreach (var t in objetivos) Purificar(t);
                break;
        }
        return totalDano;
    }

    private static List<PeleadorEstado> Objetivos(EstadoPartida s, string duenoId, PeleadorEstado lanzador,
        Objetivo objetivo, List<PeleadorEstado> aliados, List<PeleadorEstado> enemigos)
    {
        return objetivo switch
        {
            Objetivo.Propio => new List<PeleadorEstado> { lanzador },
            Objetivo.Aliado => aliados.OrderBy(a => (double)a.Vida / a.VidaMax).Take(1).ToList(),
            Objetivo.Aliados => aliados,
            Objetivo.Enemigos => enemigos.Where(e => !Tiene(e, Efecto.Invisible)).ToList(),
            Objetivo.Enemigo => SeleccionEnemigo(enemigos, lanzador.Casilla),
            _ => new List<PeleadorEstado>()
        };
    }

    private static List<PeleadorEstado> SeleccionEnemigo(List<PeleadorEstado> enemigos, int casilla)
    {
        var provocador = enemigos.FirstOrDefault(e => Tiene(e, Efecto.Provocar) && !Tiene(e, Efecto.Invisible));
        var elegido = provocador ?? enemigos.FirstOrDefault(e => e.Casilla == casilla && !Tiene(e, Efecto.Invisible))
            ?? enemigos.FirstOrDefault(e => !Tiene(e, Efecto.Invisible));
        return elegido == null ? new List<PeleadorEstado>() : new List<PeleadorEstado> { elegido };
    }

    private static PeleadorEstado? Primero(List<PeleadorEstado> valores) => valores.FirstOrDefault();

    private static int Golpear(EstadoPartida s, string duenoId, PeleadorEstado lanzador,
        IEnumerable<PeleadorEstado?> objetivos, int valor, double factor, bool fuego, List<string> log)
    {
        var total = 0;
        foreach (var objetivo in objetivos.Where(x => x != null && x.EstaVivo).Cast<PeleadorEstado>().Distinct().ToList())
        {
            if (Tiene(lanzador, Efecto.Punteria) == false && Falla(s, lanzador, objetivo))
            {
                log.Add($"  {Catalogo.Heroe(Catalogo.Heroe(lanzador.HeroeId).Id).Nombre} falla el ataque.");
                continue;
            }
            var dano = Math.Max(0, (int)Math.Round(valor * factor, MidpointRounding.AwayFromZero));
            if (fuego && Tiene(objetivo, Efecto.MarcaPolvora)) dano = (int)Math.Round(dano * 1.5, MidpointRounding.AwayFromZero);
            total += RecibirDano(s, duenoId, lanzador, objetivo, dano, false, log);
        }
        return total;
    }

    private static bool Falla(EstadoPartida s, PeleadorEstado atacante, PeleadorEstado objetivo)
    {
        var ceguera = atacante.Buffs.Where(b => b.Rondas > 0 && (b.Tipo == Efecto.Ceguera.ToString() || b.Tipo == Efecto.Confusion.ToString()))
            .Select(b => b.Valor <= 0 ? 50 : b.Valor).DefaultIfEmpty(0).Max();
        if (ceguera > 0 && Siguiente(s, 100) < ceguera) return true;
        var esquiva = Tiene(objetivo, Efecto.Lentitud) ? 0 : Math.Clamp(SumaBuffs(objetivo, Efecto.Velocidad), 0, 40);
        return esquiva > 0 && Siguiente(s, 100) < esquiva;
    }

    private static int RecibirDano(EstadoPartida s, string atacanteId, PeleadorEstado? atacante,
        PeleadorEstado objetivo, int dano, bool reflejo, List<string> log)
    {
        if (!objetivo.EstaVivo || dano <= 0) return 0;
        var bloqueo = objetivo.Buffs.FirstOrDefault(b => b.Tipo == Efecto.Impedir.ToString() && b.Rondas > 0 && b.Valor > 0);
        if (bloqueo != null && !reflejo)
        {
            bloqueo.Valor--;
            if (bloqueo.Valor <= 0) objetivo.Buffs.Remove(bloqueo);
            log.Add($"  {Catalogo.Heroe(objetivo.HeroeId).Nombre} bloquea el ataque.");
            return 0;
        }
        if (!reflejo && objetivo.ProtectorJugadorId.Length > 0)
        {
            var protector = s.Jugadores.TryGetValue(objetivo.ProtectorJugadorId, out var pj)
                ? pj.Campo.FirstOrDefault(p => p.HeroeId == objetivo.ProtectorHeroeId && p.EstaVivo) : null;
            if (protector != null && protector != objetivo)
            {
                log.Add($"  {Catalogo.Heroe(protector.HeroeId).Nombre} recibe el daño al proteger a {Catalogo.Heroe(objetivo.HeroeId).Nombre}.");
                return RecibirDano(s, atacanteId, atacante, protector, dano, false, log);
            }
            objetivo.ProtectorJugadorId = "";
            objetivo.ProtectorHeroeId = "";
        }
        if (Tiene(objetivo, Efecto.Invisible) || Tiene(objetivo, Efecto.Inmune)) return 0;
        var defensa = Math.Clamp(SumaBuffs(objetivo, Efecto.BuffDef), -50, 75);
        var reducido = Math.Max(0, (int)Math.Round(dano * (1.0 - defensa / 100.0), MidpointRounding.AwayFromZero));
        if (reducido == 0) return 0;

        var duenoObjetivo = s.Jugadores.Values.First(j => j.Campo.Contains(objetivo));
        if (!reflejo && Catalogo.Heroe(objetivo.HeroeId).Pasiva == Efecto.ContadorDevolver && atacanteId != duenoObjetivo.Id)
            objetivo.StacksDevolver++;
        if (!reflejo && atacanteId != duenoObjetivo.Id)
            foreach (var aliado in duenoObjetivo.Campo.Where(p => p.EstaVivo && Catalogo.Heroe(p.HeroeId).Pasiva == Efecto.ContadorSangre))
                aliado.StacksSangre++;

        var aplicado = Math.Min(reducido, objetivo.Vida);
        objetivo.Vida = Math.Max(0, objetivo.Vida - reducido);
        log.Add($"  {Catalogo.Heroe(objetivo.HeroeId).Nombre} recibe {aplicado} (vida {objetivo.Vida}/{objetivo.VidaMax})");
        if (!reflejo && objetivo.EstaVivo && atacante != null && atacante.EstaVivo)
        {
            var fx = objetivo.Buffs.FirstOrDefault(b => b.Tipo == Efecto.Reflejo.ToString() && b.Rondas > 0);
            if (fx != null)
                RecibirDano(s, duenoObjetivo.Id, objetivo, atacante, (int)Math.Round(reducido * fx.Valor / 100.0), true, log);
        }
        return aplicado;
    }

    private static bool PuedeActuar(PeleadorEstado p) =>
        !Tiene(p, Efecto.Paralisis) && !Tiene(p, Efecto.Incapacitar);

    private static bool Tiene(PeleadorEstado p, Efecto efecto) =>
        p.Buffs.Any(b => b.Tipo == efecto.ToString() && b.Rondas > 0);

    private static int SumaBuffs(PeleadorEstado p, Efecto efecto) =>
        p.Buffs.Where(b => b.Tipo == efecto.ToString() && b.Rondas > 0).Sum(b => b.Valor);

    private static void AnadirEfecto(PeleadorEstado? p, Efecto efecto, int valor, int duracion)
    {
        if (p == null || efecto == Efecto.Ninguno) return;
        var dur = duracion <= 0 && (efecto == Efecto.Veneno || efecto == Efecto.Sangrado || efecto == Efecto.Quemadura
            || efecto == Efecto.Maldicion || efecto == Efecto.HotZona) ? 2 : Math.Max(1, duracion);
        p.Buffs.RemoveAll(b => b.Tipo == efecto.ToString());
        p.Buffs.Add(new Buff { Tipo = efecto.ToString(), Valor = valor, Rondas = dur });
    }

    private static void PonerBuff(PeleadorEstado? p, Efecto efecto, int valor, int duracion, List<string> log)
    {
        if (p == null || !p.EstaVivo) return;
        AnadirEfecto(p, efecto, valor, duracion);
        log.Add($"  {Catalogo.Heroe(p.HeroeId).Nombre}: {efecto} {(valor >= 0 ? "+" : "")}{valor}% ({Math.Max(1, duracion)} ronda/s)");
    }

    private static void Purificar(PeleadorEstado p)
    {
        var negativos = new HashSet<string>
        {
            Efecto.Veneno.ToString(), Efecto.Sangrado.ToString(), Efecto.Quemadura.ToString(), Efecto.Maldicion.ToString(),
            Efecto.Ceguera.ToString(), Efecto.Confusion.ToString(), Efecto.Paralisis.ToString(), Efecto.Incapacitar.ToString(),
            Efecto.MarcaPolvora.ToString(), Efecto.Lentitud.ToString()
        };
        p.Buffs.RemoveAll(b => negativos.Contains(b.Tipo) || (b.Tipo == Efecto.BuffAtk.ToString() && b.Valor < 0));
    }

    private static void TickEstados(EstadoPartida s, string jugadorId, PeleadorEstado p, List<string> log)
    {
        foreach (var fx in p.Buffs.Where(b => b.Rondas > 0).ToList())
        {
            if (fx.Tipo == Efecto.Veneno.ToString() || fx.Tipo == Efecto.Sangrado.ToString()
                || fx.Tipo == Efecto.Quemadura.ToString() || fx.Tipo == Efecto.Maldicion.ToString())
                RecibirDano(s, jugadorId, null, p, fx.Valor, true, log);
            else if (fx.Tipo == Efecto.HotZona.ToString()) Curar(p, fx.Valor, log);
        }
    }

    private static void Curar(PeleadorEstado? objetivo, int cantidad, List<string> log)
    {
        if (objetivo == null || !objetivo.EstaVivo) return;
        objetivo.Vida = Math.Min(objetivo.VidaMax, objetivo.Vida + cantidad);
        log.Add($"  {Catalogo.Heroe(objetivo.HeroeId).Nombre} se cura {cantidad} (vida {objetivo.Vida}/{objetivo.VidaMax})");
    }

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
