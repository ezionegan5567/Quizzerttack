// ============ LAB12: HUD DEL EVENTO (paso 7) ============
// Cartel construido en tiempo de ejecución (sin prefabs): arriba a la derecha muestra
// el nombre del evento, el tiempo restante y los números que el servidor está aplicando.
// Si no hay evento, lo dice: el juego sigue con los números base del S10.
//
// Montaje: HudEventoLab12.CrearSiFalta();  (o arrastrar el componente a un GameObject)

#if UGS_ON
using Lab09;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudEventoLab12 : MonoBehaviour
{
    public static HudEventoLab12 Instancia { get; private set; }

    [Header("Tamaño del cartel")]
    public Vector2 tamano = new Vector2(440f, 96f);
    public float margen = 16f;

    TextMeshProUGUI _texto;

    public static HudEventoLab12 CrearSiFalta()
    {
        if (Instancia != null) return Instancia;
        var go = new GameObject("HUD Lab12");
        DontDestroyOnLoad(go);
        return go.AddComponent<HudEventoLab12>();
    }

    void Awake()
    {
        Instancia = this;
        Construir();
        EventoLiveOps.EventoCambio += Refrescar;
    }

    void OnDestroy()
    {
        EventoLiveOps.EventoCambio -= Refrescar;
        if (Instancia == this) Instancia = null;
    }

    // La cuenta atrás cambia cada segundo: se repinta por frame (es un solo texto).
    void Update()
    {
        if (_texto != null) _texto.text = Texto();
    }

    void Refrescar(VistaEventoDto vista) { if (_texto != null) _texto.text = Texto(); }

    string Texto()
    {
        if (!EventoLiveOps.HayEvento) return "SIN EVENTO\nse juega con los números base";

        var campana = EventoLiveOps.Campana;
        long mago = EventoLiveOps.PrecioMostrado("mago", 1200);
        string linea = $"{campana.Nombre}\n" +
                       $"quedan {EventoLiveOps.CuentaAtras(EventoLiveOps.SegundosRestantesAhora())}  ·  " +
                       $"daño x{campana.FactorDano:0.##}  ·  tienda -{campana.Descuento * 100:0.#} %\n" +
                       $"Mago {mago} (antes 1200)";
        if (campana.BonoBienvenida > 0)
            linea += $"  ·  bienvenida +{campana.BonoBienvenida} {campana.MonedaBono}";
        return linea;
    }

    void Construir()
    {
        var canvasGo = new GameObject("HUD Lab12 Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var escalador = canvasGo.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("Banner evento", typeof(Image));
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.SetParent(canvasGo.transform, false);
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-margen, -margen);
        panelRt.sizeDelta = tamano;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.85f);

        var textoGo = new GameObject("Texto", typeof(TextMeshProUGUI));
        var textoRt = textoGo.GetComponent<RectTransform>();
        textoRt.SetParent(panel.transform, false);
        textoRt.anchorMin = Vector2.zero;
        textoRt.anchorMax = Vector2.one;
        textoRt.offsetMin = new Vector2(14f, 10f);
        textoRt.offsetMax = new Vector2(-14f, -10f);

        _texto = textoGo.GetComponent<TextMeshProUGUI>();
        _texto.fontSize = 19f;
        _texto.alignment = TextAlignmentOptions.TopRight;
        _texto.color = new Color(0.96f, 0.86f, 0.55f, 1f);
        _texto.raycastTarget = false;
        if (_texto.font == null)
        {
            _texto.font = TMP_Settings.defaultFontAsset;
            if (_texto.font == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fuentes != null && fuentes.Length > 0) _texto.font = fuentes[0];
            }
        }
        _texto.text = Texto();
    }
}
#endif
