using System;
using System.Collections.Generic;
using System.Linq;

// ============ MODO ONLINE EN LA ESCENA REAL: LÓGICA PURA (sin UnityEngine) ============
// Decide QUÉ hay que cambiar en la escena para que se parezca a la vista del servidor, pero no toca la
// escena: devuelve una lista de acciones y EscenaOnline.cs las ejecuta. Está separada a propósito para
// poder probarla con pruebas normales (TestProject la compila, igual que PartidaDto.cs).
//
// Regla: aquí NO se decide nada del juego. Sólo se traduce "lo que dice el servidor" a "lo que hay que dibujar".
// Escrito en C# 9 (sin record ni namespace de archivo) porque Unity no compila más.

namespace QuizzerAttack.Partida
{
    public static class EscenaLogica
    {
        /// <summary>
        /// El id del servidor a partir del nombre de la carta de la escena ("Guerrero del hacha" -> "guerrero_del_hacha").
        /// El servidor genera sus ids con esta misma regla desde DatosFacciones, y una prueba comprueba las 10 cartas de la escena.
        /// </summary>
        public static string NombreAId(string nombre)
        {
            return (nombre ?? "").Trim().ToLowerInvariant().Replace(' ', '_');
        }

        // ---------------------------------------------------------------- lados de la escena

        /// <summary>Una carta de la escena, reducida a lo que importa para decidir de qué lado está cada facción.</summary>
        public class CartaEscena
        {
            public string Faccion = "";
            public int JugadorDueno;
        }

        /// <summary>
        /// La escena tiene dos manos fijas: las cartas de F1 son del jugador 1 y las de F2 del jugador 2. Cada jugador
        /// online usa la mano que corresponde a SU facción, así que las dos facciones deben existir en la escena y ser distintas.
        /// Devuelve false (y un mensaje para mostrar) si no se puede.
        /// </summary>
        public static bool ResolverLados(string miFaccion, string rivalFaccion, IList<CartaEscena> cartas,
            out int ladoMio, out int ladoRival, out string error)
        {
            ladoMio = LadoDe(miFaccion, cartas);
            ladoRival = LadoDe(rivalFaccion, cartas);
            error = "";

            if (ladoMio == 0 || ladoRival == 0)
            {
                error = "La escena sólo tiene cartas de F1 y F2. Creen la partida con F1 y únanse con F2 (o al revés).";
                return false;
            }
            if (ladoMio == ladoRival)
            {
                error = "Los dos jugadores usan la misma facción y la escena sólo tiene una mano por facción. Usen F1 contra F2.";
                return false;
            }
            return true;
        }

        private static int LadoDe(string faccion, IList<CartaEscena> cartas)
        {
            if (string.IsNullOrEmpty(faccion)) return 0;
            foreach (CartaEscena c in cartas)
                if (string.Equals(c.Faccion, faccion, StringComparison.OrdinalIgnoreCase)) return c.JugadorDueno;
            return 0;
        }

        // ---------------------------------------------------------------- qué cambiar en el campo

        /// <summary>Un peleador que existe ahora mismo en la escena.</summary>
        public class PeleadorEscena
        {
            public int Lado;
            public string HeroeId = "";
            public int Casilla;
            public int Vida;
            public int VidaMax;
            public bool Preparada;
        }

        public enum TipoAccion { Colocar, Actualizar, Quitar }

        public class Accion
        {
            public TipoAccion Tipo;
            public int Lado;
            public string HeroeId = "";
            public int Casilla;
            public int Vida;
            public int VidaMax;
            public bool Preparada;
        }

        /// <summary>
        /// Compara la vista del servidor con los peleadores de la escena y dice qué colocar, qué actualizar y qué quitar.
        /// Es idempotente: si la escena ya coincide devuelve una lista vacía, así que se puede llamar cada pocos frames.
        /// De los peleadores del rival sólo se sabe lo que el servidor deja ver: la habilidad preparada nunca se pinta.
        /// </summary>
        public static List<Accion> Planificar(VistaPartidaDto v, int ladoMio, int ladoRival, IList<PeleadorEscena> escena)
        {
            List<Accion> acciones = new List<Accion>();
            if (v == null) return acciones;

            PlanificarLado(acciones, v.MiCampo, ladoMio, true, escena);
            PlanificarLado(acciones, v.RivalCampo, ladoRival, false, escena);
            return acciones;
        }

        private static void PlanificarLado(List<Accion> acciones, List<PeleadorDto> campo, int lado, bool propio,
            IList<PeleadorEscena> escena)
        {
            foreach (PeleadorDto p in campo)
            {
                bool preparada = propio && p.HabilidadPreparada >= 0;
                PeleadorEscena actual = escena.FirstOrDefault(e => e.Lado == lado && e.HeroeId == p.HeroeId);

                if (actual == null)
                {
                    acciones.Add(new Accion { Tipo = TipoAccion.Colocar, Lado = lado, HeroeId = p.HeroeId, Casilla = p.Casilla,
                        Vida = p.Vida, VidaMax = p.VidaMax, Preparada = preparada });
                }
                else if (actual.Vida != p.Vida || actual.VidaMax != p.VidaMax || actual.Preparada != preparada)
                {
                    acciones.Add(new Accion { Tipo = TipoAccion.Actualizar, Lado = lado, HeroeId = p.HeroeId, Casilla = actual.Casilla,
                        Vida = p.Vida, VidaMax = p.VidaMax, Preparada = preparada });
                }
            }

            // Lo que está en la escena y el servidor ya no tiene en el campo (cayó en la pelea).
            foreach (PeleadorEscena e in escena.Where(x => x.Lado == lado))
                if (!campo.Any(p => p.HeroeId == e.HeroeId))
                    acciones.Add(new Accion { Tipo = TipoAccion.Quitar, Lado = lado, HeroeId = e.HeroeId, Casilla = e.Casilla });
        }

        // ---------------------------------------------------------------- qué puede hacer el jugador

        /// <summary>¿Se puede colocar este héroe ahora? Sólo para pintar el botón: el servidor vuelve a comprobarlo todo.</summary>
        public static bool PuedeColocar(VistaPartidaDto v, string heroeId)
        {
            return v != null && v.Estado == EstadoPartidaValores.EnJuego && v.EsTuTurno
                && v.Fase == FasePartidaValores.Colocar && v.Pregunta == null && v.MiMano.Contains(heroeId);
        }

        /// <summary>¿Se puede elegir habilidad con este peleador ahora? Igual: sólo para la interfaz.</summary>
        public static bool PuedeElegirHabilidad(VistaPartidaDto v, string heroeId)
        {
            if (v == null || v.Estado != EstadoPartidaValores.EnJuego || !v.EsTuTurno) return false;
            if (v.Fase != FasePartidaValores.Habilidades || v.Pregunta != null) return false;
            return v.MiCampo.Any(p => p.HeroeId == heroeId && p.HabilidadPreparada < 0 && p.HabilidadesElegibles.Count > 0);
        }

        /// <summary>¿Se puede pasar el turno? (no con una pregunta pendiente, ni fuera de turno).</summary>
        public static bool PuedePasar(VistaPartidaDto v)
        {
            return v != null && v.Estado == EstadoPartidaValores.EnJuego && v.EsTuTurno && v.Pregunta == null;
        }

        // ---------------------------------------------------------------- textos

        public static string TextoTurno(VistaPartidaDto v)
        {
            if (v == null) return "Sin partida online.";
            if (v.Estado == EstadoPartidaValores.EsperandoRival) return "Esperando a que el rival se una. Código: " + v.Codigo;

            if (v.Estado == EstadoPartidaValores.Terminada)
            {
                if (string.IsNullOrEmpty(v.GanadorId)) return "Fin de la partida: empate o cancelada (" + v.MotivoFin + ").";
                return v.GanadorId == v.TuId ? "¡GANASTE! (" + v.MotivoFin + ")" : "Perdiste (" + v.MotivoFin + ").";
            }

            string ronda = "Ronda " + v.Ronda + " · ";
            if (!v.EsTuTurno) return ronda + "turno del rival…";
            if (v.Pregunta != null) return ronda + "responde la pregunta";
            return v.Fase == FasePartidaValores.Colocar
                ? ronda + "tu turno: elige UNA carta (o pasa)"
                : ronda + "tu turno: clic en un peleador para elegir su habilidad";
        }

        /// <summary>Segundos que quedan según el plazo del servidor (la hora local sólo se usa para la cuenta atrás visual).</summary>
        public static int SegundosRestantes(VistaPartidaDto v, DateTime ahoraUtc)
        {
            return v == null ? 0 : SegundosHasta(v.DeadlineUtc, ahoraUtc);
        }

        /// <summary>Segundos hasta una hora ISO del servidor (0 si está vacía o ya pasó).</summary>
        public static int SegundosHasta(string isoUtc, DateTime ahoraUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return 0;
            DateTime limite = DateTime.Parse(isoUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
            double s = (limite - ahoraUtc).TotalSeconds;
            return s < 0 ? 0 : (int)s;
        }
    }
}
