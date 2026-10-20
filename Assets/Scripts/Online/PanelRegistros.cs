using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============ ONLINE: PANEL DE REGISTROS PÚBLICOS ============
// UI que lista jugadores registrados en la nube y permite:
//   - Publicar mi escuadrón (subir datos de mis 5 cartas).
//   - Simular: pelea automática contra ese escuadrón (resultado en texto).
//   - Combatir: carga el escuadrón enemigo como P2 para jugar Fase 1-5 real.
// Se abre con teclaAbrir (F9). Crear un GameObject vacío "Registros" en la
// escena y ponerle este script: solo eso, no hace falta nada más.

public class PanelRegistros : MonoBehaviour
{
    public static PanelRegistros Instance;

    [Header("Config")]
    public string miNombre = "Jugador1";
    public KeyCode teclaAbrir = KeyCode.F9;
    [Range(0, 8)] public int filasVisibles = 8;

    [Header("Auto (no tocar)")]
    public GameObject panelUI;
    public ClienteNube cliente;

    GestorTurnos gestor;
    TextMeshProUGUI textoEstado;
    TextMeshProUGUI textoResultado;
    List<Transform> filas = new List<Transform>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        gestor = UnityEngine.Object.FindFirstObjectByType<GestorTurnos>();
        cliente = UnityEngine.Object.FindFirstObjectByType<ClienteNube>();
        if (cliente == null) cliente = gameObject.AddComponent<ClienteNube>();
        CrearUI();
    }

    void Update()
    {
        bool abrir = false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = UnityEngine.InputSystem.Keyboard.current;
        abrir = kb != null && kb.f9Key.wasPressedThisFrame;
#else
        abrir = Input.GetKeyDown(teclaAbrir);
#endif
        if (abrir) Toggle();
    }

    public void Toggle()
    {
        if (panelUI == null) return;
        bool abrir = !panelUI.activeSelf;
        panelUI.SetActive(abrir);
        panelUI.transform.SetAsLastSibling();
        if (abrir) Abrir();
        else Cursor.visible = true;
    }

    public void Abrir()
    {
        if (panelUI == null) return;
        panelUI.SetActive(true);
        panelUI.transform.SetAsLastSibling();
        LimpiarFilas();
        if (textoEstado != null) textoEstado.text = "Cargando registros...";
        cliente.DescargarRegistros(Poblar);
    }

    void CrearUI()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject goCanvas = new GameObject("CanvasRegistros");
            canvas = goCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            goCanvas.AddComponent<CanvasScaler>();
            goCanvas.AddComponent<GraphicRaycaster>();
        }

        panelUI = new GameObject("PanelRegistros");
        panelUI.transform.SetParent(canvas.transform, false);
        Image fondo = panelUI.AddComponent<Image>();
        fondo.color = new Color(0.05f, 0.08f, 0.16f, 0.95f);
        RectTransform rt = panelUI.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(430f, 560f);

        Canvas cv = panelUI.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 250;
        panelUI.AddComponent<GraphicRaycaster>();

        CrearTextoFijo("Titulo", panelUI.transform, "REGISTROS PUBLICOS",
            new Vector2(0, 248), new Vector2(380, 30), 20, Color.yellow);
        textoEstado = CrearTextoFijo("Estado", panelUI.transform, "",
            new Vector2(0, 222), new Vector2(380, 20), 13, Color.cyan);

        Button btnActualizar = CrearBotonFijo("BtnActualizar", panelUI.transform,
            "Actualizar registros", new Vector2(-105, 192), new Vector2(190, 30),
            new Color(0.2f, 0.4f, 0.75f, 1f));
        btnActualizar.onClick.AddListener(Abrir);

        Button btnPublicar = CrearBotonFijo("BtnPublicar", panelUI.transform,
            "Publicar mi escuadrón", new Vector2(105, 192), new Vector2(190, 30),
            new Color(0.2f, 0.55f, 0.28f, 1f));
        btnPublicar.onClick.AddListener(PublicarMio);

        textoResultado = CrearTextoFijo("Resultado", panelUI.transform,
            "Publica tu escuadrón y simula contra otros.",
            new Vector2(0, -230), new Vector2(390, 52), 12, Color.white);
        textoResultado.alignment = TextAlignmentOptions.Top;

        Button btnCerrar = CrearBotonFijo("BtnCerrar", panelUI.transform,
            "CERRAR (F9)", new Vector2(0, -262), new Vector2(150, 26), new Color(0.4f, 0.4f, 0.4f, 1f));
        btnCerrar.onClick.AddListener(Toggle);

        panelUI.SetActive(false);
    }

    void Poblar(List<RegistroDefensivo> lista)
    {
        LimpiarFilas();
        if (textoEstado != null)
            textoEstado.text = lista == null
                ? "Sin conexión / sin datos aún."
                : lista.Count + " jugador(es) en la nube.";
        if (lista == null || panelUI == null) return;

        int i = 0;
        foreach (RegistroDefensivo r in lista.OrderByDescending(x => x.fecha))
        {
            if (i >= filasVisibles) break;
            string fecha = r.fecha > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(r.fecha).LocalDateTime.ToString("dd/MM HH:mm")
                : "?";
            int n = r.peleadores != null ? r.peleadores.Count : 0;
            string linea = $"{r.jugador}\n{r.faccion}  {n} pelea.  {fecha}";
            CrearTexto("Fila" + i, panelUI.transform, linea,
                new Vector2(-65, 150 - i * 46), new Vector2(240, 40), 11, Color.white);

            Button bs = CrearBoton("Sim" + i, panelUI.transform, "Simular",
                new Vector2(95, 156 - i * 46), new Vector2(78, 28),
                new Color(0.25f, 0.4f, 0.75f, 1f));
            RegistroDefensivo reg = r; // copia local para lambda
            bs.onClick.AddListener(() =>
            {
                textoEstado.text = "Simulando contra " + reg.jugador + "...";
                panelUI.transform.SetAsLastSibling();
                string res = SimuladorBatalla.Simular(reg);
                if (textoResultado != null) textoResultado.text = res;
                if (textoEstado != null) textoEstado.text = "Simulación terminada.";
            });

            Button bc = CrearBoton("Com" + i, panelUI.transform, "Combatir",
                new Vector2(165, 156 - i * 46), new Vector2(78, 28),
                new Color(0.65f, 0.3f, 0.22f, 1f));
            bc.onClick.AddListener(() => CombatirCon(reg));

            if (textoResultado != null) textoResultado.transform.SetAsLastSibling();
            i++;
        }
        panelUI.transform.SetAsLastSibling();
    }

    void PublicarMio()
    {
        if (gestor == null) gestor = UnityEngine.Object.FindFirstObjectByType<GestorTurnos>();
        if (gestor == null || gestor.cartasPlayer1 == null)
        {
            if (textoEstado != null) textoEstado.text = "No encuentro las cartas P1.";
            return;
        }
        string nombre = RegistroDefensivo.Sanear(miNombre);
        RegistroDefensivo reg = RegistroDefensivo.DesdeCartas(nombre, gestor.faccionP1, gestor.cartasPlayer1);
        if (textoEstado != null) textoEstado.text = "Publicando escuadrón...";
        cliente.SubirRegistro(reg, ok =>
        {
            if (textoEstado != null)
                textoEstado.text = ok
                    ? $"Publicado como {nombre} ({reg.peleadores.Count} peleadores)."
                    : "Fallo al publicar (¿URL configurada?)";
        });
    }

    void CombatirCon(RegistroDefensivo reg)
    {
        if (gestor == null) gestor = UnityEngine.Object.FindFirstObjectByType<GestorTurnos>();
        if (gestor == null || gestor.cartasPlayer2 == null || reg == null || reg.peleadores == null)
        {
            if (textoResultado != null) textoResultado.text = "No puedo cargar el escuadrón (faltan cartas P2).";
            return;
        }
        int n = Mathf.Min(reg.peleadores.Count, gestor.cartasPlayer2.Length);
        for (int i = 0; i < gestor.cartasPlayer2.Length; i++)
        {
            if (gestor.cartasPlayer2[i] == null) continue;
            CartaSeleccion c = gestor.cartasPlayer2[i].GetComponent<CartaSeleccion>();
            if (c == null) continue;
            c.ReiniciarCarta();
            if (i < n)
            {
                PeleadorRegistro pr = reg.peleadores[i];
                c.nombreHeroe = string.IsNullOrEmpty(pr.nombre) ? "Misterio" : pr.nombre;
                if (pr.vida > 0) c.vida = pr.vida;
                if (pr.ataque > 0) c.ataque = pr.ataque;
                c.imagenPeleador = null;
            }
            c.jugadorDueno = 2;
            c.faccion = reg.faccion;
            c.RefrescarSprite();
            c.RefrescarVisual();
        }
        gestor.faccionP2 = reg.faccion;
        gestor.ReiniciarContraRegistro();
        if (textoResultado != null)
            textoResultado.text = $"Escuadrón de {reg.jugador} cargado como P2 ({reg.faccion}). ¡Fase 1!";
        Toggle();
    }

    void LimpiarFilas()
    {
        foreach (Transform t in filas)
            if (t != null) Destroy(t.gameObject);
        filas.Clear();
    }

    TextMeshProUGUI CrearTextoFijo(string nombre, Transform padre, string contenido, Vector2 pos, Vector2 size, int fs, Color c)
    {
        TextMeshProUGUI tmp = CrearTexto(nombre, padre, contenido, pos, size, fs, c);
        // Los fijos no se borran al recargar filas: quitarlos de la lista.
        filas.Remove(tmp.transform);
        return tmp;
    }

    Button CrearBotonFijo(string nombre, Transform padre, string texto, Vector2 pos, Vector2 size, Color c)
    {
        Button b = CrearBoton(nombre, padre, texto, pos, size, c);
        filas.Remove(b.transform);
        return b;
    }

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, Vector2 pos, Vector2 size, int fs, Color c)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = contenido;
        tmp.fontSize = fs;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = c;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fuentes != null && fuentes.Length > 0) tmp.font = fuentes[0];
            }
        }
        filas.Add(go.transform);
        return tmp;
    }

    Button CrearBoton(string nombre, Transform padre, string texto, Vector2 pos, Vector2 size, Color c)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = c;
        Button btn = go.AddComponent<Button>();

        GameObject t = new GameObject("Texto");
        t.transform.SetParent(go.transform, false);
        RectTransform trt = t.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 13;
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
        filas.Add(go.transform);
        return btn;
    }
}
