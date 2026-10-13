using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GestorTurnos : MonoBehaviour
{
    [Header("Manos de cada jugador")]
    public GameObject[] cartasPlayer1;
    public GameObject[] cartasPlayer2;

    [Header("UI")]
    public TextMeshProUGUI textoTurno;
    public Button botonPasarTurno;
    public Temporizador temporizador;
    public TextMeshProUGUI textoResultado;

    [Header("Fases: 1=coloca primero, 2=coloca segundo, 3=hab primero, 4=hab segundo, 5=pelea")]
    public int fase = 0;

    [Header("Orden por moneda")]
    public int jugadorInicial = 1;

    [Header("Facción fija por jugador")]
    public string faccionP1 = "F1";
    public string faccionP2 = "F2";

    public bool PuedeUsarCarta(int dueno, string facc)
    {
        string propia = (dueno == 1) ? faccionP1 : faccionP2;
        if (string.IsNullOrEmpty(facc)) return true;
        return string.Equals(propia.Trim(), facc.Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    private bool juegoTerminado = false;
    private bool esperandoMoneda = true;

    public bool faseHabilidades
    {
        get { return fase == 3 || fase == 4; }
        set { fase = value ? 3 : 1; }
    }

    private int turnoActual = 1;

    void Start()
    {
        fase = 0;
        esperandoMoneda = true;
        if (temporizador == null)
            temporizador = Object.FindFirstObjectByType<Temporizador>();
        if (temporizador != null) temporizador.PausarTemporizador();
        AsignarDuenos();
        SetCartas(cartasPlayer1, false);
        SetCartas(cartasPlayer2, false);
        ApagarTodasLasCartas();
        if (textoTurno != null) textoTurno.text = "Lanzando moneda...";

        if (botonPasarTurno != null)
        {
            botonPasarTurno.onClick.AddListener(PasarTurno);
            botonPasarTurno.interactable = false;
        }
    }

    int SegundoJugador() { return jugadorInicial == 1 ? 2 : 1; }

    public void IniciarJuego(int primero)
    {
        jugadorInicial = (primero == 2) ? 2 : 1;
        juegoTerminado = false;
        esperandoMoneda = false;
        if (botonPasarTurno != null) botonPasarTurno.interactable = true;
        fase = 1;
        MostrarTurno(jugadorInicial);
        Debug.Log("Empieza Player " + jugadorInicial + ", segundo Player " + SegundoJugador());
    }

    public void PasarTurno()
    {
        if (juegoTerminado || esperandoMoneda) return;
        if (fase == 1)
        {
            fase = 2;
            MostrarTurno(SegundoJugador());
        }
        else if (fase == 2)
        {
            fase = 3;
            SetCartas(cartasPlayer1, false);
            SetCartas(cartasPlayer2, false);
            MostrarTurno(jugadorInicial);
            Debug.Log("Fase 3: habilidades Player " + jugadorInicial);
        }
        else if (fase == 3)
        {
            fase = 4;
            MostrarTurno(SegundoJugador());
            Debug.Log("Fase 4: habilidades Player " + SegundoJugador());
        }
        else if (fase == 4)
        {
            IniciarPelea();
        }
        else if (fase == 5)
        {
            VerificarFin();
        }
    }

    public void IniciarFaseHabilidades()
    {
        fase = 3;
        // Apagar todas las cartas, ya no se usan
        SetCartas(cartasPlayer1, false);
        SetCartas(cartasPlayer2, false);
        esperandoMoneda = false;
        MostrarTurno(jugadorInicial);
    }

    public void IniciarFaseColocacion()
    {
        fase = 1;
        esperandoMoneda = false;
        MostrarTurno(jugadorInicial);
    }

    void MostrarTurno(int jugador)
    {
        turnoActual = jugador;

        if (temporizador == null)
            temporizador = Object.FindFirstObjectByType<Temporizador>();
        if (temporizador != null)
            temporizador.IniciarTemporizador();

        if (fase == 1 || fase == 2)
        {
            bool tocaP1 = (turnoActual == 1);
            SetCartas(cartasPlayer1, tocaP1);
            SetCartas(cartasPlayer2, !tocaP1);
            // Respaldo: toda carta de la escena responde a su dueño en su turno
            CartaSeleccion[] escena = Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None);
            foreach (CartaSeleccion s in escena)
            {
                if (s == null) continue;
                Button b = s.GetComponent<Button>();
                if (b == null) continue;
                if (s.EstaUsada()) { b.interactable = false; continue; }
                b.interactable = (s.jugadorDueno == turnoActual);
                if (b.interactable) s.RefrescarVisual();
            }
            Debug.Log($"MostrarTurno fase {fase} turno P{jugador}: P1array={cartasPlayer1?.Length} P2array={cartasPlayer2?.Length}");
        }

        if (textoTurno != null)
        {
            if (fase == 1)
                textoTurno.text = $"Fase 1: Player {turnoActual} coloca - elige tu carta";
            else if (fase == 2)
                textoTurno.text = $"Fase 2: Player {turnoActual} coloca - elige tu carta";
            else if (fase == 3)
                textoTurno.text = $"Fase 3: Player {turnoActual} - elige habilidad";
            else if (fase == 4)
                textoTurno.text = $"Fase 4: Player {turnoActual} - elige habilidad";
        }

        Debug.Log("Ahora es turno del Player " + jugador);
    }

    void SetCartas(GameObject[] cartas, bool activo)
    {
        if (cartas == null) return;

        foreach (GameObject carta in cartas)
        {
            if (carta == null) continue;

            // Si la carta ya fue usada, no la reactivamos
            CartaSeleccion sel = carta.GetComponent<CartaSeleccion>();
            if (sel != null && sel.EstaUsada())
            {
                // La dejamos visible en rojo pero sin interaccion
                Button btn = carta.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
                continue;
            }

            Button b = carta.GetComponent<Button>();
            if (b != null) b.interactable = activo;
            if (activo) sel.RefrescarVisual();

            // Opcional: atenuar mano inactiva sin apagarla
            // carta.SetActive(activo);
        }
    }

    void ApagarTodasLasCartas()
    {
        CartaSeleccion[] todas = Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None);
        foreach (CartaSeleccion s in todas)
        {
            if (s == null) continue;
            Button b = s.GetComponent<Button>();
            if (b != null) b.interactable = false;
        }
    }

    void AsignarDuenos()
    {
        if (cartasPlayer1 != null)
            foreach (GameObject c in cartasPlayer1)
            {
                if (c == null) continue;
                CartaSeleccion s = c.GetComponent<CartaSeleccion>();
                if (s != null) s.jugadorDueno = 1;
            }
        if (cartasPlayer2 != null)
            foreach (GameObject c in cartasPlayer2)
            {
                if (c == null) continue;
                CartaSeleccion s = c.GetComponent<CartaSeleccion>();
                if (s != null) s.jugadorDueno = 2;
            }
        // Respaldo por nombre para las cartas de la escena
        CartaSeleccion[] escena = Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None);
        foreach (CartaSeleccion s in escena)
        {
            if (s == null) continue;
            string n = s.gameObject.name.ToLower();
            if (n.Contains("player 2")) s.jugadorDueno = 2;
            else if (n.Contains("player 1")) s.jugadorDueno = 1;
        }
    }

    public int GetTurnoActual()
    {
        return turnoActual;
    }

    void IniciarPelea()
    {
        fase = 5;
        if (textoTurno != null) textoTurno.text = "Fase 5: ¡PELEA AUTOMÁTICA!";
        if (temporizador != null) temporizador.PausarTemporizador();
        Debug.Log("Fase 5: pelea automática con habilidades preparadas.");

        Peleador[] todos = Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None);
        System.Collections.Generic.List<Peleador> orden = new System.Collections.Generic.List<Peleador>(todos);
        orden.Sort((a, b) =>
        {
            int d = a.jugadorDueno.CompareTo(b.jugadorDueno);
            return d != 0 ? d : a.indiceCasilla.CompareTo(b.indiceCasilla);
        });
        foreach (Peleador p in orden)
            if (p != null && p.HabilidadLista() && p.PuedePelear())
                p.EjecutarGuardada();

        Debug.Log("Fin de pelea automática. Pasar = verificar.");
    }

    void ResolverPelea()
    {
        IniciarPelea();
    }

    void VerificarFin()
    {
        Peleador[] todos = Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None);
        int vivosP1 = 0, vivosP2 = 0;
        foreach (Peleador p in todos)
        {
            if (p == null || !p.EstaVivo()) continue;
            if (p.jugadorDueno == 1) vivosP1++;
            else vivosP2++;
        }

        int cartasP1 = Mathf.Max(ContarCartasSinUsar(cartasPlayer1), ContarCartasEscena(1));
        int cartasP2 = Mathf.Max(ContarCartasSinUsar(cartasPlayer2), ContarCartasEscena(2));
        Debug.Log($"Fin pelea: P1 vivos={vivosP1} cartas={cartasP1} | P2 vivos={vivosP2} cartas={cartasP2}");

        bool sigueP1 = vivosP1 > 0 || cartasP1 > 0;
        bool sigueP2 = vivosP2 > 0 || cartasP2 > 0;

        if (!sigueP1 && !sigueP2) { TerminarJuego("Empate: nadie tiene cartas ni guerreros"); return; }
        if (!sigueP1) { TerminarJuego("¡Player 2 GANA! P1 sin cartas ni guerreros"); return; }
        if (!sigueP2) { TerminarJuego("¡Player 1 GANA! P2 sin cartas ni guerreros"); return; }

        // Si hay peleadores, nueva vuelta como PvZ Heroes
        NuevaRonda();
    }

    int ContarCartasSinUsar(GameObject[] cartas)
    {
        if (cartas == null) return 0;
        int n = 0;
        foreach (GameObject c in cartas)
        {
            if (c == null) continue;
            CartaSeleccion s = c.GetComponent<CartaSeleccion>();
            if (s != null && !s.EstaUsada()) n++;
        }
        return n;
    }

    int ContarCartasEscena(int dueno)
    {
        int n = 0;
        CartaSeleccion[] escena = Object.FindObjectsByType<CartaSeleccion>(FindObjectsSortMode.None);
        foreach (CartaSeleccion s in escena)
        {
            if (s == null || s.EstaUsada()) continue;
            if (s.jugadorDueno == dueno) n++;
        }
        return n;
    }

    void NuevaRonda()
    {
        Debug.Log("Nueva ronda: Fase 1");
        Peleador.LimpiarEstaticos();
        Peleador[] todos = Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None);
        foreach (Peleador p in todos)
            if (p != null) p.TickRonda();
        todos = Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None);
        foreach (Peleador p in todos)
            if (p != null) p.ReiniciarPeleador();
        fase = 1;
        MostrarTurno(jugadorInicial);
    }

    void TerminarJuego(string mensaje)
    {
        juegoTerminado = true;
        fase = 5;
        if (textoTurno != null) textoTurno.text = mensaje;
        if (textoResultado != null) textoResultado.text = mensaje;
        if (temporizador != null) temporizador.PausarTemporizador();
        if (botonPasarTurno != null) botonPasarTurno.interactable = false;
        Debug.Log("FIN: " + mensaje);
    }
}
