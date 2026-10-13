using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Peleador : MonoBehaviour
{
    [System.Serializable]
    public class ActiveFx
    {
        public EfectoH tipo;
        public int valor;
        public int turnos;
        public ActiveFx(EfectoH t, int v, int d) { tipo = t; valor = v; turnos = d; }
    }

    [Header("Dueño y carril")]
    public int jugadorDueno = 1;
    public int indiceCasilla = 0;
    public string faccion = "F1";

    [Header("Datos (nombre busca habilidades en DatosFacciones)")]
    public string nombrePeleador = "Guerrero";
    public int vida = 100;
    public int vidaMax = 100;
    public int ataque = 25;
    public string[] habilidades = { "Golpe fuerte" };
    public string fortalezas = "Fuego";
    public string debilidades = "Agua";

    [Header("Tipos legacy (ya no se usan, ver DatosFacciones)")]
    public TipoHabilidad[] tiposHabilidad = { TipoHabilidad.DanoUno };

    [Header("Panel auto (sin referencias)")]
    public float alturaSobrePeleador = 1.8f;
    public Vector2 tamanoPanel = new Vector2(240f, 170f);

    [Header("Panel dentro del Canvas (opcional, se autocrea si None)")]
    public GameObject panelHabilidades;
    public TextMeshProUGUI textoNombre;
    public TextMeshProUGUI textoVida;
    public TextMeshProUGUI textoAtaque;
    public TextMeshProUGUI textoHabilidades;
    public Button[] botonesHabilidad;

    [Header("Color al usar habilidad / seleccionado pelea")]
    public Color colorHabilidadUsada = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color colorAtacanteSel = Color.yellow;

    [Header("Quiz: buff / debuff al valor de la habilidad")]
    [Range(0f, 1f)] public float porcentajeAcierto = 0.10f;
    [Range(0f, 1f)] public float porcentajeFallo = 0.10f;

    public enum TipoHabilidad { DanoUno, DanoTodos, BuffEquipo }

    private static Peleador seleccionadoActual = null;
    private static Peleador atacanteSel = null;
    private static Peleador lanzadorPendiente = null;

    private bool fijado = false;
    private bool habilidadUsada = false;
    private bool habilidadEjecutada = false;
    private int indiceHabilidadElegida = -1;
    private int habPendiente = -1;
    private float multPendiente = 1f;
    private float multGuardado = 1f;
    public bool atacoEnPelea = false;

    private int ataqueBase;
    private Color colorOriginal;
    private SpriteRenderer sr;
    private GestorTurnos gestor;
    private RectTransform panelRect;
    private Image barraFill;
    private RectTransform barraFillRt;

    private InfoHabilidad[] habsData;
    private List<ActiveFx> fx = new List<ActiveFx>();
    private bool tieneDevolver = false;
    private bool tieneSangre = false;
    private int stacksDevolver = 0;
    private int stacksSangre = 0;
    private Peleador protector = null;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        colorOriginal = sr.color;
        if (GetComponent<Collider2D>() == null)
            gameObject.AddComponent<BoxCollider2D>();
        gestor = Object.FindFirstObjectByType<GestorTurnos>();
    }

    void OnValidate()
    {
        DatosPeleador d = DatosFacciones.Get(nombrePeleador);
        if (d != null && d.habs != null)
        {
            habilidades = new string[d.habs.Length];
            for (int i = 0; i < d.habs.Length; i++)
                habilidades[i] = d.habs[i].nombre;
        }
        int[] st = DatosFacciones.GetStats(nombrePeleador);
        if (st != null) { vida = st[0]; ataque = st[1]; vidaMax = vida; }
    }

    void Start()
    {
        int[] st = DatosFacciones.GetStats(nombrePeleador);
        if (st != null) { vida = st[0]; ataque = st[1]; }
        vidaMax = vida;
        ataqueBase = ataque;

        DatosPeleador d = DatosFacciones.Get(nombrePeleador);
        if (d != null)
        {
            habilidades = new string[d.habs.Length];
            habsData = new InfoHabilidad[d.habs.Length];
            for (int i = 0; i < d.habs.Length; i++)
            {
                habilidades[i] = d.habs[i].nombre;
                habsData[i] = d.habs[i];
            }
            if (d.pasiva == EfectoH.ContadorDevolver) tieneDevolver = true;
            if (d.pasiva == EfectoH.ContadorSangre) tieneSangre = true;
        }
        else
        {
            habsData = new InfoHabilidad[] {
                new InfoHabilidad("Golpe fuerte", EfectoH.Dano, ObjetivoH.Enemigo, 20)
            };
        }

        if (panelHabilidades == null)
            CrearPanelAutomatico();
        else
        {
            Canvas c = panelHabilidades.GetComponent<Canvas>();
            if (c == null) c = panelHabilidades.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 100;
            panelRect = panelHabilidades.GetComponent<RectTransform>();
            panelHabilidades.SetActive(false);
        }
        ConectarBotones();
        CrearBarraVida();
    }

    void CrearBarraVida()
    {
        GameObject barra = new GameObject("BarraVida");
        barra.transform.SetParent(transform, false);
        barra.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        barra.transform.localScale = Vector3.one * 0.01f;
        Canvas cv = barra.AddComponent<Canvas>();
        cv.renderMode = RenderMode.WorldSpace;
        cv.sortingOrder = 50;
        RectTransform brt = barra.GetComponent<RectTransform>();
        brt.sizeDelta = new Vector2(120f, 12f);

        GameObject fondo = new GameObject("Fondo");
        fondo.transform.SetParent(barra.transform, false);
        RectTransform frt = fondo.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        Image fimg = fondo.AddComponent<Image>();
        fimg.color = new Color(0f, 0f, 0f, 0.7f);

        GameObject relleno = new GameObject("Relleno");
        relleno.transform.SetParent(barra.transform, false);
        barraFillRt = relleno.AddComponent<RectTransform>();
        barraFill = relleno.AddComponent<Image>();
        ActualizarBarra();
    }

    void ActualizarBarra()
    {
        if (barraFill == null || barraFillRt == null) return;
        float pct = vidaMax > 0 ? Mathf.Clamp01((float)vida / vidaMax) : 0f;
        barraFillRt.anchorMin = Vector2.zero;
        barraFillRt.anchorMax = new Vector2(pct, 1f);
        barraFillRt.offsetMin = Vector2.zero;
        barraFillRt.offsetMax = Vector2.zero;
        barraFill.color = pct > 0.5f ? Color.green : (pct > 0.25f ? Color.yellow : Color.red);
    }

    // ============ TURNO / FASE ============
    bool EsMiTurno()
    {
        if (gestor == null)
            gestor = Object.FindFirstObjectByType<GestorTurnos>();
        if (gestor == null) return true;
        if (!gestor.faseHabilidades) return false;
        return gestor.GetTurnoActual() == jugadorDueno;
    }

    bool PuedeActuar()
    {
        return !Tiene(EfectoH.Paralisis) && !Tiene(EfectoH.Incapacitar);
    }

    // ============ HOVER / CLICK ============
    void OnMouseEnter()
    {
        if (habilidadUsada) return;
        if (seleccionadoActual != null && seleccionadoActual != this) return;
        if (!EsMiTurno()) return;
        MostrarPanel();
    }

    void OnMouseExit()
    {
        if (fijado) return;
        OcultarPanel();
    }

    void OnMouseDown()
    {
        if (gestor == null) gestor = Object.FindFirstObjectByType<GestorTurnos>();

        if (gestor != null && gestor.fase == 5)
        {
            Debug.Log("La pelea es automática.");
            return;
        }

        if (habilidadUsada) return;
        if (!PuedeActuar()) { Debug.Log(nombrePeleador + " no puede actuar (controlado)."); return; }
        if (!EsMiTurno()) return;
        if (seleccionadoActual != null && seleccionadoActual != this) return;

        fijado = true;
        seleccionadoActual = this;
        MostrarPanel();
        Debug.Log(nombrePeleador + " seleccionado, elige habilidad. Los demás bloqueados.");
    }

    // ============ ELEGIR HABILIDAD + QUIZ ============
    public void ElegirHabilidad(int indice)
    {
        if (!fijado) return;
        if (habsData == null || indice < 0 || indice >= habsData.Length) return;

        indiceHabilidadElegida = indice;

        QuizHabilidades quiz = QuizHabilidades.Instance;
        if (quiz == null)
            quiz = Object.FindFirstObjectByType<QuizHabilidades>();

        if (quiz == null)
        {
            TrasQuiz(1f, indice);
            return;
        }
        quiz.MostrarQuiz(this, indice, (correcta) =>
            TrasQuiz(correcta ? (1f + porcentajeAcierto) : (1f - porcentajeFallo), indice));
        Debug.Log(nombrePeleador + " responde quiz para: " + habsData[indice].nombre);
    }

    void TrasQuiz(float mult, int indice)
    {
        // Ronda 3/4 = preparación: se guarda, NO daña todavía.
        indiceHabilidadElegida = indice;
        multGuardado = mult;
        habilidadUsada = true;
        fijado = false;
        habPendiente = -1;
        if (seleccionadoActual == this) seleccionadoActual = null;
        if (lanzadorPendiente == this) lanzadorPendiente = null;
        if (sr != null) sr.color = colorHabilidadUsada;
        OcultarPanel();
        Debug.Log(nombrePeleador + " preparó " + habsData[indice].nombre +
            (mult >= 1f ? " (quiz +buff)" : " (quiz -debuff)") + ". Peleará en Fase 5.");
    }

    public bool PuedePelear() { return EstaVivo() && PuedeActuar(); }

    public void EjecutarGuardada()
    {
        if (!habilidadUsada || habilidadEjecutada) return;
        if (habsData == null || indiceHabilidadElegida < 0 || indiceHabilidadElegida >= habsData.Length) return;
        InfoHabilidad hab = habsData[indiceHabilidadElegida];
        Peleador objetivo = null;
        if (hab.objetivo == ObjetivoH.Enemigo)
            objetivo = EnemigoEnCarril(indiceCasilla);
        else if (hab.objetivo == ObjetivoH.Aliado)
            objetivo = AliadoMasDanado();
        habilidadEjecutada = true;
        EjecutarSobre(objetivo, indiceHabilidadElegida, multGuardado);
    }

    Peleador EnemigoEnCarril(int carril)
    {
        Peleador primero = null;
        Peleador carrilero = null;
        Peleador taunt = null;
        foreach (Peleador p in Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None))
        {
            if (p == null || !p.EstaVivo() || p.jugadorDueno == jugadorDueno) continue;
            if (p.Tiene(EfectoH.Invisible)) continue;
            if (primero == null) primero = p;
            if (p.Tiene(EfectoH.Provocar) && taunt == null) taunt = p;
            if (p.indiceCasilla == carril && carrilero == null) carrilero = p;
        }
        if (taunt != null) return taunt;
        return carrilero != null ? carrilero : primero;
    }

    Peleador AliadoMasDanado()
    {
        Peleador peor = this;
        float peorPct = 2f;
        foreach (Peleador p in EquipoAliado())
        {
            float pct = p.vidaMax > 0 ? (float)p.vida / p.vidaMax : 2f;
            if (pct < peorPct) { peorPct = pct; peor = p; }
        }
        return peor;
    }

    void IntentarRecibirHabilidad()
    {
        Peleador lanz = lanzadorPendiente;
        if (lanz == null || !lanz.EstaVivo()) { lanzadorPendiente = null; return; }
        InfoHabilidad hab = lanz.habsData[lanz.habPendiente];
        string error = "";
        bool ok = false;
        if (hab.objetivo == ObjetivoH.Enemigo)
        {
            if (jugadorDueno == lanz.jugadorDueno) error = "debe ser un enemigo";
            else if (!EstaVivo()) error = "está muerto";
            else if (Tiene(EfectoH.Invisible)) error = "invisible, no se puede elegir";
            else if (HayTauntEnemigoDe(lanz) && !Tiene(EfectoH.Provocar)) error = "hay provocador: ataca al taunt";
            else ok = true;
        }
        else
        {
            if (jugadorDueno != lanz.jugadorDueno) error = "debe ser un aliado";
            else if (!EstaVivo()) error = "está muerto";
            else ok = true;
        }
        if (!ok)
        {
            Debug.Log($"No puedes usar {hab.nombre} en {nombrePeleador}: {error}.");
            return;
        }
        lanzadorPendiente = null;
        lanz.EjecutarSobre(this, lanz.habPendiente, lanz.multPendiente);
    }

    bool HayTauntEnemigoDe(Peleador lanzador)
    {
        Peleador[] todos = Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None);
        foreach (Peleador p in todos)
            if (p != null && p != lanzador && p.jugadorDueno != lanzador.jugadorDueno
                && p.EstaVivo() && p.Tiene(EfectoH.Provocar) && !p.Tiene(EfectoH.Invisible))
                return true;
        return false;
    }

    // ============ CAST ============
    public void EjecutarSobre(Peleador objetivo, int idx, float mult)
    {
        if (habsData == null || idx < 0 || idx >= habsData.Length) return;
        InfoHabilidad hab = habsData[idx];

        int dealt = 0;
        dealt += AplicarEfecto(hab.efecto, hab.objetivo,
            Mathf.RoundToInt(hab.valor * mult), hab.duracion, objetivo, hab);
        if (hab.efecto2 != EfectoH.Ninguno)
        {
            ObjetivoH o2 = hab.objetivo2 == ObjetivoH.Ninguno ? hab.objetivo : hab.objetivo2;
            dealt += AplicarEfecto(hab.efecto2, o2,
                Mathf.RoundToInt(hab.valor2 * mult), hab.duracion2, objetivo, hab);
        }

        // Contadores: perforación consume devolución, sangre consume stacks
        if (tieneDevolver && stacksDevolver > 0 && hab.nombre.ToLower().Contains("devol"))
        {
            Debug.Log(nombrePeleador + " devuelve +" + stacksDevolver + " stacks (se resetea).");
            stacksDevolver = 0;
        }
        if (tieneSangre && stacksSangre > 0 && hab.nombre.ToLower().Contains("sangre"))
        {
            int bonus = stacksSangre * 5;
            if (objetivo != null && objetivo.EstaVivo() && hab.objetivo == ObjetivoH.Enemigo)
                dealt += objetivo.RecibirDano(bonus, this);
            Debug.Log(nombrePeleador + " sangre consume stacks +" + bonus + " (se resetea).");
            stacksSangre = 0;
        }
        if (tieneDevolver && stacksDevolver > 0 && hab.nombre.ToLower().Contains("perfor"))
        {
            int bonus = stacksDevolver * Mathf.RoundToInt(ataqueBase * 0.25f);
            if (objetivo != null && hab.objetivo == ObjetivoH.Enemigo)
                dealt += objetivo.RecibirDano(bonus, this);
            Debug.Log(nombrePeleador + " perforación devuelve +" + bonus + " (se resetea).");
            stacksDevolver = 0;
        }

        if (hab.robo > 0f && dealt > 0)
            Curar(this, Mathf.RoundToInt(dealt * hab.robo));

        TerminarUso();
        Debug.Log(nombrePeleador + " usó " + hab.nombre + ".");
    }

    void TerminarUso()
    {
        habilidadUsada = true;
        fijado = false;
        habPendiente = -1;
        if (seleccionadoActual == this) seleccionadoActual = null;
        if (lanzadorPendiente == this) lanzadorPendiente = null;
        if (sr != null) sr.color = colorHabilidadUsada;
        OcultarPanel();
    }

    public void CancelarSeleccion()
    {
        if (lanzadorPendiente == this) lanzadorPendiente = null;
        if (!fijado) return;
        fijado = false;
        habPendiente = -1;
        if (seleccionadoActual == this) seleccionadoActual = null;
        if (sr != null) sr.color = EstaVivo() ? colorOriginal : sr.color;
        OcultarPanel();
    }

    int AplicarEfecto(EfectoH ef, ObjetivoH ob, int valor, int dur, Peleador objetivo, InfoHabilidad hab)
    {
        if (ef == EfectoH.Ninguno) return 0;
        List<Peleador> targets = new List<Peleador>();
        if (ob == ObjetivoH.Enemigo) { if (objetivo != null) targets.Add(objetivo); }
        else if (ob == ObjetivoH.Aliado) { if (objetivo != null) targets.Add(objetivo); }
        else if (ob == ObjetivoH.Propio) targets.Add(this);
        else if (ob == ObjetivoH.Enemigos)
        {
            foreach (Peleador p in EquipoEnemigo())
                if (!p.Tiene(EfectoH.Invisible)) targets.Add(p);
        }
        else if (ob == ObjetivoH.Aliados) targets.AddRange(EquipoAliado());

        int dealt = 0;
        switch (ef)
        {
            case EfectoH.Dano:
                foreach (Peleador t in targets)
                {
                    if (!ValidoEnemigo(t)) continue;
                    if (FallaContra(t)) continue;
                    int dmg = valor;
                    if (t.Tiene(EfectoH.MarcaPolvora) && hab.fuego)
                    {
                        dmg = Mathf.RoundToInt(dmg * 1.5f);
                        Debug.Log("¡Polvora + fuego! x1.5 en " + t.nombrePeleador);
                    }
                    dealt += t.RecibirDano(dmg, this);
                }
                break;
            case EfectoH.DanoTodos:
                foreach (Peleador t in EquipoEnemigo())
                {
                    if (t.Tiene(EfectoH.Invisible)) continue;
                    if (FallaContra(t)) continue;
                    int dmg = valor;
                    if (t.Tiene(EfectoH.MarcaPolvora) && hab.fuego) dmg = Mathf.RoundToInt(dmg * 1.5f);
                    dealt += t.RecibirDano(dmg, this);
                }
                break;
            case EfectoH.DanoDos:
                {
                    int n = 0;
                    if (objetivo != null && ValidoEnemigo(objetivo) && !FallaContra(objetivo)) { dealt += objetivo.RecibirDano(valor, this); n++; }
                    if (n > 0)
                        foreach (Peleador t in EquipoEnemigo())
                        {
                            if (t == objetivo || !ValidoEnemigo(t)) continue;
                            if (FallaContra(t)) continue;
                            dealt += t.RecibirDano(valor, this);
                            break;
                        }
                }
                break;
            case EfectoH.Veneno:
            case EfectoH.Sangrado:
            case EfectoH.Quemadura:
            case EfectoH.Maldicion:
                foreach (Peleador t in targets) t.AddFx(ef, valor, dur);
                break;
            case EfectoH.Curar:
                foreach (Peleador t in targets) Curar(t, valor);
                break;
            case EfectoH.CurarZona:
                foreach (Peleador t in EquipoAliado()) Curar(t, valor);
                break;
            case EfectoH.HotZona:
                foreach (Peleador t in EquipoAliado()) t.AddFx(EfectoH.HotZona, valor, dur);
                break;
            case EfectoH.BuffAtk:
                foreach (Peleador t in targets) { t.AddFx(EfectoH.BuffAtk, valor, dur); t.RecalcularAtaque(); }
                break;
            case EfectoH.BuffAtkZona:
                foreach (Peleador t in EquipoAliado()) { t.AddFx(EfectoH.BuffAtk, valor, dur); t.RecalcularAtaque(); }
                break;
            case EfectoH.BuffDef:
            case EfectoH.BuffDefZona:
                {
                    List<Peleador> ts = (ef == EfectoH.BuffDefZona) ? EquipoAliado() : targets;
                    foreach (Peleador t in ts) t.AddFx(EfectoH.BuffDef, valor, dur);
                }
                break;
            case EfectoH.Velocidad:
                foreach (Peleador t in targets) t.AddFx(EfectoH.Velocidad, valor, dur);
                Debug.Log("(Velocidad: esquiva " + valor + "%.)");
                break;
            case EfectoH.VelocidadZona:
                foreach (Peleador t in EquipoAliado()) t.AddFx(EfectoH.Velocidad, valor, dur);
                break;
            case EfectoH.Punteria:
                foreach (Peleador t in targets) t.AddFx(EfectoH.Punteria, 0, dur);
                break;
            case EfectoH.Lentitud:
                foreach (Peleador t in targets) { t.AddFx(EfectoH.Lentitud, valor, dur); t.RecalcularAtaque(); }
                break;
            case EfectoH.Intimidar:
                foreach (Peleador t in targets) { t.AddFx(EfectoH.BuffAtk, -valor, dur); t.RecalcularAtaque(); }
                break;
            case EfectoH.Ceguera:
            case EfectoH.Confusion:
            case EfectoH.Paralisis:
            case EfectoH.Incapacitar:
            case EfectoH.Invisible:
            case EfectoH.Provocar:
            case EfectoH.Inmune:
            case EfectoH.MarcaPolvora:
            case EfectoH.Reflejo:
                foreach (Peleador t in targets) t.AddFx(ef, valor, dur);
                break;
            case EfectoH.InmuneZona:
                foreach (Peleador t in EquipoAliado()) t.AddFx(EfectoH.Inmune, 0, dur);
                break;
            case EfectoH.Impedir:
                foreach (Peleador t in targets) t.AddFx(EfectoH.Impedir, 1, dur);
                break;
            case EfectoH.Proteger:
                foreach (Peleador t in targets)
                {
                    if (t == this) continue;
                    t.protector = this;
                    t.AddFx(EfectoH.Proteger, 0, dur);
                    Debug.Log(nombrePeleador + " protege a " + t.nombrePeleador);
                }
                break;
            case EfectoH.Purificar:
                foreach (Peleador t in targets) t.Purificar();
                break;
        }
        return dealt;
    }

    bool ValidoEnemigo(Peleador t)
    {
        if (t == null || !t.EstaVivo()) return false;
        if (t.jugadorDueno == jugadorDueno) return false;
        if (t.Tiene(EfectoH.Invisible)) return false;
        return true;
    }

    List<Peleador> EquipoAliado()
    {
        List<Peleador> r = new List<Peleador>();
        foreach (Peleador p in Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None))
            if (p != null && p.EstaVivo() && p.jugadorDueno == jugadorDueno) r.Add(p);
        return r;
    }

    List<Peleador> EquipoEnemigo()
    {
        List<Peleador> r = new List<Peleador>();
        foreach (Peleador p in Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None))
            if (p != null && p.EstaVivo() && p.jugadorDueno != jugadorDueno) r.Add(p);
        return r;
    }

    // ============ FX ============
    public bool Tiene(EfectoH tipo)
    {
        foreach (ActiveFx f in fx)
            if (f.tipo == tipo && f.turnos > 0) return true;
        return false;
    }

    void AddFx(EfectoH tipo, int valor, int dur)
    {
        if (tipo == EfectoH.Ninguno) return;
        if (dur <= 0 && (tipo == EfectoH.Veneno || tipo == EfectoH.Sangrado || tipo == EfectoH.Quemadura
            || tipo == EfectoH.Maldicion || tipo == EfectoH.HotZona)) dur = 2;
        fx.RemoveAll(f => f.tipo == tipo);
        fx.Add(new ActiveFx(tipo, valor, Mathf.Max(dur, 1)));
        Debug.Log(nombrePeleador + " gana efecto " + tipo + " (" + valor + ") " + dur + "T.");
    }

    void Purificar()
    {
        fx.RemoveAll(f => f.tipo == EfectoH.Veneno || f.tipo == EfectoH.Sangrado
            || f.tipo == EfectoH.Quemadura || f.tipo == EfectoH.Maldicion
            || f.tipo == EfectoH.Ceguera || f.tipo == EfectoH.Confusion
            || f.tipo == EfectoH.Paralisis || f.tipo == EfectoH.Incapacitar
            || f.tipo == EfectoH.MarcaPolvora || f.tipo == EfectoH.Lentitud
            || (f.tipo == EfectoH.BuffAtk && f.valor < 0));
        RecalcularAtaque();
        Debug.Log(nombrePeleador + " purificado.");
    }

    void RecalcularAtaque()
    {
        int pct = 0;
        foreach (ActiveFx f in fx)
        {
            if (f.turnos <= 0) continue;
            if (f.tipo == EfectoH.BuffAtk) pct += f.valor;
            else if (f.tipo == EfectoH.Lentitud) pct -= f.valor;
        }
        ataque = Mathf.RoundToInt(ataqueBase * (1f + pct / 100f));
    }

    int EsquivaPct()
    {
        if (Tiene(EfectoH.Lentitud)) return 0;
        int e = 0;
        foreach (ActiveFx f in fx)
            if (f.tipo == EfectoH.Velocidad && f.turnos > 0) e += f.valor;
        return Mathf.Clamp(e, 0, 40);
    }

    bool FallaContra(Peleador objetivo)
    {
        if (Tiene(EfectoH.Punteria)) return false;
        int miss = 0;
        foreach (ActiveFx f in fx)
        {
            if (f.turnos <= 0) continue;
            if (f.tipo == EfectoH.Ceguera || f.tipo == EfectoH.Confusion)
                miss = Mathf.Max(miss, f.valor <= 0 ? 50 : f.valor);
        }
        if (miss > 0 && Random.value * 100f < miss)
        {
            Debug.Log(nombrePeleador + " falla (" + miss + "% ciego/confundido).");
            return true;
        }
        if (objetivo != null)
        {
            int dodge = objetivo.EsquivaPct();
            if (dodge > 0 && Random.value * 100f < dodge)
            {
                Debug.Log(objetivo.nombrePeleador + " esquiva (" + dodge + "% velocidad).");
                return true;
            }
        }
        return false;
    }

    int DefensaPct()
    {
        int pct = 0;
        foreach (ActiveFx f in fx)
            if (f.tipo == EfectoH.BuffDef && f.turnos > 0) pct += f.valor;
        return Mathf.Clamp(pct, -50, 75);
    }

    public void TickRonda()
    {
        List<ActiveFx> dots = new List<ActiveFx>(fx);
        foreach (ActiveFx f in dots)
        {
            if (f.tipo == EfectoH.Veneno || f.tipo == EfectoH.Sangrado
                || f.tipo == EfectoH.Quemadura || f.tipo == EfectoH.Maldicion)
                RecibirDano(f.valor, null, true);
            else if (f.tipo == EfectoH.HotZona)
                Curar(this, f.valor);
        }
        for (int i = fx.Count - 1; i >= 0; i--)
        {
            fx[i].turnos--;
            if (fx[i].turnos <= 0) fx.RemoveAt(i);
        }
        if (protector != null && !protector.EstaVivo()) protector = null;
        RecalcularAtaque();
    }

    public static void LimpiarEstaticos()
    {
        seleccionadoActual = null;
        atacanteSel = null;
        lanzadorPendiente = null;
    }

    // ============ DAÑO / CURA ============
    public bool EstaVivo() { return vida > 0; }

    void Curar(Peleador t, int cantidad)
    {
        if (t == null || !t.EstaVivo()) return;
        t.vida = Mathf.Min(t.vidaMax, t.vida + cantidad);
        t.ActualizarBarra();
        Debug.Log(t.nombrePeleador + " recupera +" + cantidad + " (vida " + t.vida + ").");
    }

    public int RecibirDano(int dano, Peleador atacante = null, bool esReflejo = false)
    {
        if (!EstaVivo() || dano <= 0) return 0;

        ActiveFx imp = fx.Find(f => f.tipo == EfectoH.Impedir && f.turnos > 0);
        if (imp != null && !esReflejo)
        {
            imp.valor--;
            if (imp.valor <= 0) fx.Remove(imp);
            Debug.Log(nombrePeleador + " impide el ataque (bloqueo).");
            return 0;
        }

        if (protector != null && protector != this && protector.EstaVivo() && !esReflejo)
        {
            Debug.Log(protector.nombrePeleador + " recibe el daño en lugar de " + nombrePeleador);
            return protector.RecibirDano(dano, atacante, false);
        }
        if (protector != null && !protector.EstaVivo()) protector = null;

        if (Tiene(EfectoH.Invisible) || Tiene(EfectoH.Inmune))
        {
            Debug.Log(nombrePeleador + " ignora el daño.");
            return 0;
        }

        int reducido = Mathf.RoundToInt(dano * (1f - DefensaPct() / 100f));
        reducido = Mathf.Max(reducido, 0);

        if (tieneDevolver && atacante != null && atacante.jugadorDueno != jugadorDueno)
        {
            stacksDevolver++;
            Debug.Log(nombrePeleador + " acumula devolución (" + stacksDevolver + ").");
        }
        if (atacante != null && atacante.jugadorDueno != jugadorDueno)
        {
            foreach (Peleador p in EquipoAliado())
                if (p.tieneSangre && p.EstaVivo())
                {
                    p.stacksSangre++;
                    Debug.Log(p.nombrePeleador + " sangre carga (" + p.stacksSangre + ").");
                }
        }

        int vidaAntes = vida;
        vida -= reducido;
        int dealt = Mathf.Min(reducido, vidaAntes);
        ActualizarBarra();
        Debug.Log(nombrePeleador + " (P" + jugadorDueno + ") recibe " + reducido + ". Vida: " + Mathf.Max(vida, 0));
        if (vida <= 0)
        {
            vida = 0;
            Debug.Log(nombrePeleador + " murió.");
            if (seleccionadoActual == this) seleccionadoActual = null;
            if (atacanteSel == this) atacanteSel = null;
            if (lanzadorPendiente == this) lanzadorPendiente = null;
            if (panelHabilidades != null) panelHabilidades.SetActive(false);
            Destroy(gameObject);
        }
        else if (!esReflejo)
        {
            ActiveFx refx = fx.Find(f => f.tipo == EfectoH.Reflejo && f.turnos > 0);
            if (refx != null && atacante != null && atacante.EstaVivo())
            {
                int dev = Mathf.RoundToInt(reducido * refx.valor / 100f);
                Debug.Log(nombrePeleador + " refleja " + dev + " a " + atacante.nombrePeleador);
                atacante.RecibirDano(dev, this, true);
            }
        }
        return dealt;
    }

    public TipoHabilidad GetTipoActual() { return TipoHabilidad.DanoUno; }
    public string GetHabilidadElegida()
    {
        if (habsData == null || indiceHabilidadElegida < 0 || indiceHabilidadElegida >= habsData.Length) return "";
        return habsData[indiceHabilidadElegida].nombre;
    }
    public bool HabilidadLista() { return habilidadUsada; }

    // ============ PELEA FASE 5 (1v1 por carril) ============
    void ClickPelea()
    {
        if (!EstaVivo() || atacoEnPelea) return;
        if (!PuedeActuar()) { Debug.Log(nombrePeleador + " no puede pelear (controlado)."); return; }

        if (atacanteSel != null && atacanteSel != this && atacanteSel.jugadorDueno != jugadorDueno)
        {
            if (indiceCasilla != atacanteSel.indiceCasilla && HayEnemigoEnCarril(atacanteSel))
            {
                Debug.Log($"Carril {atacanteSel.indiceCasilla + 1}: ataca a tu enfrente, no a la casilla {indiceCasilla + 1}.");
                return;
            }
            atacanteSel.AtacarBasico(this);
            return;
        }

        if (HabilidadLista())
        {
            if (atacanteSel != null && atacanteSel != this)
                atacanteSel.sr.color = atacanteSel.colorHabilidadUsada;
            atacanteSel = this;
            sr.color = colorAtacanteSel;
            Debug.Log(nombrePeleador + $" (carril {indiceCasilla + 1}) listo: clic al enemigo de enfrente.");
        }
    }

    bool HayEnemigoEnCarril(Peleador atacante)
    {
        foreach (Peleador p in Object.FindObjectsByType<Peleador>(FindObjectsSortMode.None))
            if (p != null && p != atacante && p.jugadorDueno != atacante.jugadorDueno
                && p.indiceCasilla == atacante.indiceCasilla && p.EstaVivo()
                && !p.Tiene(EfectoH.Invisible))
                return true;
        return false;
    }

    public void AtacarBasico(Peleador objetivo)
    {
        if (!HabilidadLista() || atacoEnPelea) return;
        if (FallaContra(objetivo))
        {
            atacoEnPelea = true;
            if (atacanteSel == this) atacanteSel = null;
            sr.color = colorHabilidadUsada;
            return;
        }
        if (objetivo != null && objetivo.EstaVivo())
            objetivo.RecibirDano(ataque, this);
        Debug.Log(nombrePeleador + " ataca 1v1 a " + (objetivo != null ? objetivo.nombrePeleador : "?") + " con " + ataque);
        atacoEnPelea = true;
        if (atacanteSel == this) atacanteSel = null;
        sr.color = colorHabilidadUsada;
    }

    public void ReiniciarPeleador()
    {
        habilidadUsada = false;
        habilidadEjecutada = false;
        multGuardado = 1f;
        fijado = false;
        indiceHabilidadElegida = -1;
        habPendiente = -1;
        multPendiente = 1f;
        atacoEnPelea = false;
        if (seleccionadoActual == this) seleccionadoActual = null;
        if (atacanteSel == this) atacanteSel = null;
        if (lanzadorPendiente == this) lanzadorPendiente = null;
        RecalcularAtaque();
        if (sr != null) sr.color = colorOriginal;
    }

    // ============ PANEL AUTO ============
    void ConectarBotones()
    {
        if (botonesHabilidad == null) return;
        for (int i = 0; i < botonesHabilidad.Length; i++)
        {
            int idx = i;
            if (botonesHabilidad[i] != null)
            {
                botonesHabilidad[i].onClick.RemoveAllListeners();
                botonesHabilidad[i].onClick.AddListener(() => ElegirHabilidad(idx));
            }
        }
    }

    void CrearPanelAutomatico()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject goCanvas = new GameObject("CanvasAuto");
            canvas = goCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            goCanvas.AddComponent<CanvasScaler>();
            goCanvas.AddComponent<GraphicRaycaster>();
        }

        int nHab = (habsData != null) ? habsData.Length : 1;
        GameObject panel = new GameObject("Panel_" + nombrePeleador);
        panel.transform.SetParent(canvas.transform, false);
        Image fondo = panel.AddComponent<Image>();
        fondo.color = new Color(0f, 0f, 0f, 0.85f);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tamanoPanel.x, tamanoPanel.y + Mathf.Max(0, nHab - 1) * 34f);
        panelRect = rt;

        Canvas cv = panel.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 100;
        panel.AddComponent<GraphicRaycaster>();

        textoNombre = CrearTexto("Nombre", panel.transform, "", new Vector2(0, 60), new Vector2(220, 30));
        textoVida = CrearTexto("Vida", panel.transform, "", new Vector2(0, 32), new Vector2(220, 26));
        textoAtaque = CrearTexto("Ataque", panel.transform, "", new Vector2(0, 8), new Vector2(220, 26));
        textoHabilidades = CrearTexto("Habs", panel.transform, "", new Vector2(0, -16), new Vector2(230, 26));

        string[] nombres = habilidades;
        if (nombres == null || nombres.Length == 0) nombres = new string[] { "Golpe fuerte" };
        botonesHabilidad = new Button[nombres.Length];
        for (int i = 0; i < nombres.Length; i++)
        {
            int idx = i;
            Button b = CrearBoton("Hab" + i, panel.transform, "Usar: " + nombres[i], new Vector2(0, -48 - i * 34));
            b.onClick.AddListener(() => ElegirHabilidad(idx));
            botonesHabilidad[i] = b;
        }

        panelHabilidades = panel;
        panel.SetActive(false);
    }

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = contenido;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fuentes != null && fuentes.Length > 0) tmp.font = fuentes[0];
            }
        }
        return tmp;
    }

    Button CrearBoton(string nombre, Transform padre, string texto, Vector2 pos)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(210, 30);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        Button btn = go.AddComponent<Button>();

        GameObject txtGo = new GameObject("Texto");
        txtGo.transform.SetParent(go.transform, false);
        RectTransform txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 15;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fuentes != null && fuentes.Length > 0) tmp.font = fuentes[0];
            }
        }
        return btn;
    }

    void OnDestroy()
    {
        if (panelHabilidades != null)
            Destroy(panelHabilidades);
        if (seleccionadoActual == this) seleccionadoActual = null;
        if (atacanteSel == this) atacanteSel = null;
        if (lanzadorPendiente == this) lanzadorPendiente = null;
    }

    void LateUpdate()
    {
        if (panelHabilidades != null && panelHabilidades.activeSelf && panelRect != null && Camera.main != null)
        {
            Vector3 mundo = transform.position + Vector3.up * alturaSobrePeleador;
            panelRect.position = Camera.main.WorldToScreenPoint(mundo);
            panelHabilidades.transform.SetAsLastSibling();
        }
    }

    void MostrarPanel()
    {
        if (panelHabilidades == null) return;
        panelHabilidades.SetActive(true);
        panelHabilidades.transform.SetAsLastSibling();
        ActualizarPanel();
    }

    void OcultarPanel()
    {
        if (fijado) return;
        if (panelHabilidades != null)
            panelHabilidades.SetActive(false);
    }

    void ActualizarPanel()
    {
        if (textoNombre != null) textoNombre.text = nombrePeleador + " (P" + jugadorDueno + ")";
        if (textoVida != null) textoVida.text = "Vida: " + vida + "/" + vidaMax;
        if (textoAtaque != null) textoAtaque.text = "Ataque: " + ataque;
        if (textoHabilidades != null && habilidades != null)
            textoHabilidades.text = string.Join(" / ", habilidades);
    }
}
