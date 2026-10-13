using UnityEngine;
using TMPro;
using System.Collections;

public class MonedaInicial : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI textoMoneda;
    public RectTransform monedaVisual;
    public GestorTurnos gestor;

    [Header("Tiempos")]
    public float duracionGiro = 2.5f;
    public float velocidadGiro = 720f;
    public float pausaResultado = 1.5f;

    void Start()
    {
        if (gestor == null)
            gestor = Object.FindFirstObjectByType<GestorTurnos>();
        if (textoMoneda == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject go = new GameObject("TextoMoneda");
                go.transform.SetParent(canvas.transform, false);
                RectTransform rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, 200);
                rt.sizeDelta = new Vector2(600, 80);
                textoMoneda = go.AddComponent<TextMeshProUGUI>();
                textoMoneda.fontSize = 36;
                textoMoneda.alignment = TextAlignmentOptions.Center;
                textoMoneda.color = Color.yellow;
                if (textoMoneda.font == null)
                {
                    textoMoneda.font = TMP_Settings.defaultFontAsset;
                    if (textoMoneda.font == null)
                    {
                        var f = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                        if (f != null && f.Length > 0) textoMoneda.font = f[0];
                    }
                }
            }
        }
        StartCoroutine(RutinaMoneda());
    }

    IEnumerator RutinaMoneda()
    {
        int primero = Random.Range(1, 3);
        int segundo = (primero == 1) ? 2 : 1;

        if (textoMoneda != null)
            textoMoneda.text = "Lanzando moneda...";

        float t = 0f;
        while (t < duracionGiro)
        {
            t += Time.deltaTime;
            if (monedaVisual != null)
                monedaVisual.Rotate(0f, velocidadGiro * Time.deltaTime, 0f);
            yield return null;
        }

        if (textoMoneda != null)
            textoMoneda.text = $"¡Player {primero} juega primero! Player {segundo} segundo.";

        Debug.Log($"Moneda: primero P{primero}, segundo P{segundo}");
        yield return new WaitForSeconds(pausaResultado);

        if (textoMoneda != null)
            textoMoneda.gameObject.SetActive(false);
        if (monedaVisual != null)
            monedaVisual.gameObject.SetActive(false);

        if (gestor != null)
            gestor.IniciarJuego(primero);
    }
}
