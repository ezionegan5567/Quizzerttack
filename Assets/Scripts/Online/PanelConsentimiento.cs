// ============ LAB13: PANTALLA DE CONSENTIMIENTO (TVYA) ============
// Aparece al arrancar la escena de juego. El SDK de Analytics queda APAGADO:
// solo se enciende (StartDataCollection) si el jugador pulsa ACEPTAR.
// ACEPTAR -> Telemetria.IniciarRecoleccion("jugador-<n>") + evento juego_iniciado.
// NO ACEPTAR -> la telemetría sigue apagada (los Registrar() se ignoran con log).
// Montaje: automático — PanelRival lo agrega al GameObject "Registros" si falta.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelConsentimiento : MonoBehaviour
{
    public static PanelConsentimiento Instancia;

    GameObject ui;
    TextMeshProUGUI estado;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
    }

    void Start()
    {
        Mostrar();
    }

    public void Mostrar()
    {
        if (Telemetria.Recolectando) return; // ya aceptó en esta sesión
        if (ui != null) { ui.SetActive(true); ui.transform.SetAsLastSibling(); return; }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject goCanvas = new GameObject("CanvasConsentimiento");
            canvas = goCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            goCanvas.AddComponent<CanvasScaler>();
            goCanvas.AddComponent<GraphicRaycaster>();
        }

        ui = new GameObject("PanelConsentimiento");
        ui.transform.SetParent(canvas.transform, false);
        Image fondo = ui.AddComponent<Image>();
        fondo.color = new Color(0.05f, 0.08f, 0.16f, 0.97f);
        var rt = ui.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(430f, 300f);

        Canvas cv = ui.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 300;
        ui.AddComponent<GraphicRaycaster>();

        var titulo = CrearTexto("Titulo", ui.transform,
            "PRIVACIDAD DEL JUEGO", new Vector2(0, 110), new Vector2(380, 30), 20, Color.yellow);
        CrearTexto("Cuerpo", ui.transform,
            "Podemos recolectar datos ANÓNIMOS de tu partida (combates, compras,\n" +
            "tiempo de arranque) para mejorar el balance del juego.\n" +
            "No se envían datos personales, solo un identificador de perfil.\n" +
            "¿Aceptás la recolección de telemetría?",
            new Vector2(0, 20), new Vector2(390, 100), 13, Color.white);

        estado = CrearTexto("Estado", ui.transform, "", new Vector2(0, -55), new Vector2(390, 24), 12, Color.cyan);

        var btnOk = CrearBoton("BtnAceptar", ui.transform, "ACEPTAR",
            new Vector2(-100, -100), new Vector2(180, 36), new Color(0.2f, 0.55f, 0.28f, 1f));
        btnOk.onClick.AddListener(Aceptar);

        var btnNo = CrearBoton("BtnRechazar", ui.transform, "NO, GRACIAS",
            new Vector2(100, -100), new Vector2(180, 36), new Color(0.45f, 0.25f, 0.25f, 1f));
        btnNo.onClick.AddListener(Rechazar);
    }

    async void Aceptar()
    {
        string id = "jugador-" + Random.Range(10, 99); // ExternalUserId propio, uno x sesión
        await Telemetria.IniciarRecoleccion(id);       // inicializa servicios + enciende SDK
        if (!Telemetria.Recolectando)
        {
            if (estado != null) estado.text = "No se pudo iniciar la telemetría (mira la consola).";
            return; // sin SDK apagado no registramos nada; el juego sigue normal
        }
        Telemetria.Registrar(Telemetria.Eventos.JuegoIniciado); // primer punto del recorrido
        if (estado != null) estado.text = "Gracias: telemetría ON (" + id + ").";
        if (ui != null) ui.SetActive(false);
    }

    void Rechazar()
    {
        Debug.Log("[DVSC] El jugador NO aceptó la telemetría: el registro queda APAGADO.");
        if (estado != null) estado.text = "Entendido: sin telemetría (el juego funciona igual).";
        if (ui != null) ui.SetActive(false);
    }

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, Vector2 pos, Vector2 size, int fs, Color c)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
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
        return tmp;
    }

    Button CrearBoton(string nombre, Transform padre, string texto, Vector2 pos, Vector2 size, Color c)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = c;
        var btn = go.AddComponent<Button>();

        GameObject t = new GameObject("Texto");
        t.transform.SetParent(go.transform, false);
        var trt = t.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 14;
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
}
