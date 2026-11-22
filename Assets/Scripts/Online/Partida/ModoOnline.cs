using System;

// ============ MODO ONLINE EN LA ESCENA REAL: GANCHOS ============
// Pequeña clase estática que une el juego local (Peleador, CartaSeleccion) con el modo online (EscenaOnline).
// Mientras Activo sea false, que es lo normal, NADA cambia en el juego local: los ganchos de Peleador y
// CartaSeleccion empiezan con "if (ModoOnline.Activo)" y no hacen nada más. No usa UGS, así que compila
// en cualquier plataforma; sólo EscenaOnline (que sí necesita UGS_ON) la rellena.

namespace QuizzerAttack.Partida
{
    public static class ModoOnline
    {
        /// <summary>true mientras la escena se dibuja desde la vista del servidor.</summary>
        public static bool Activo;

        /// <summary>(jugadorDueno, nombrePeleador) -> ¿puede este peleador elegir habilidad ahora?</summary>
        public static Func<int, string, bool> PuedeElegirHabilidad;

        /// <summary>(jugadorDueno, nombreHeroe): el jugador hizo clic en una carta de su mano.</summary>
        public static Action<int, string> AlClicCarta;

        /// <summary>(jugadorDueno, nombrePeleador, indiceHabilidad): el jugador eligió una habilidad.</summary>
        public static Action<int, string, int> AlElegirHabilidad;

        public static void Desactivar()
        {
            Activo = false;
            PuedeElegirHabilidad = null;
            AlClicCarta = null;
            AlElegirHabilidad = null;
        }
    }
}
