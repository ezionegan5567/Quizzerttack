using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

[Serializable]
public class Pregunta
{
    [TextArea] public string pregunta = "¿Cuánto es 2+2?";
    public string[] respuestas = new string[4] { "3", "4", "5", "6" };
    public int indiceCorrecta = 1;
}

public class QuizHabilidades : MonoBehaviour
{
    public static QuizHabilidades Instance;

    [Header("Banco editable desde Inspector")]
    public List<Pregunta> banco = new List<Pregunta>()
    {
        new Pregunta(){ pregunta = "¿Cuánto es 2+2?", respuestas = new string[]{"3","4","5","6"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Color del fuego?", respuestas = new string[]{"Azul","Verde","Rojo","Negro"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Cuántos días tiene una semana?", respuestas = new string[]{"5","6","7","8"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Capital de Perú?", respuestas = new string[]{"Cusco","Lima","Arequipa","Trujillo"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué hace el botón Pasar?", respuestas = new string[]{"Nada","Cambia turno","Borra carta","Sale del juego"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuánto es 5+3?", respuestas = new string[]{"7","8","9","6"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuánto es 10-4?", respuestas = new string[]{"5","7","6","4"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Cuánto es 3x2?", respuestas = new string[]{"5","6","7","8"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿De qué color es el cielo de día?", respuestas = new string[]{"Verde","Azul","Rojo","Amarillo"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿De qué color es el pasto?", respuestas = new string[]{"Rojo","Azul","Verde","Morado"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Cuántas patas tiene un perro?", respuestas = new string[]{"2","3","4","6"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Qué animal dice miau?", respuestas = new string[]{"Perro","Gato","Vaca","Pato"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué animal da leche?", respuestas = new string[]{"Gallina","Vaca","Cerdo","Oveja"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuántos meses tiene un año?", respuestas = new string[]{"10","11","12","13"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Qué viene después del lunes?", respuestas = new string[]{"Domingo","Martes","Viernes","Sábado"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿En qué planeta vivimos?", respuestas = new string[]{"Marte","Venus","Tierra","Luna"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Cuánto es 7+7?", respuestas = new string[]{"12","13","14","15"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Cuánto es 20-10?", respuestas = new string[]{"5","10","15","20"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué color sale de azul + amarillo?", respuestas = new string[]{"Verde","Morado","Naranja","Marrón"}, indiceCorrecta = 0 },
        new Pregunta(){ pregunta = "¿Qué usas para escribir?", respuestas = new string[]{"Cuchara","Lápiz","Taza","Zapato"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Con qué ves?", respuestas = new string[]{"Ojos","Manos","Pies","Orejas"}, indiceCorrecta = 0 },
        new Pregunta(){ pregunta = "¿Cuántos dedos hay en una mano?", respuestas = new string[]{"4","5","6","3"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué cae del cielo cuando llueve?", respuestas = new string[]{"Nieve","Agua","Piedras","Hojas"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿El sol sale de día o de noche?", respuestas = new string[]{"De noche","De día","Nunca","A veces"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuánto es 4+4?", respuestas = new string[]{"6","7","8","9"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Cuánto es 9-3?", respuestas = new string[]{"5","6","7","8"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué animal vuela?", respuestas = new string[]{"Perro","Pájaro","Pez","Gato"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué animal nada?", respuestas = new string[]{"Pez","Perro","Gato","Pájaro"}, indiceCorrecta = 0 },
        new Pregunta(){ pregunta = "¿De qué color es una manzana roja?", respuestas = new string[]{"Verde","Roja","Azul","Amarilla"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuántas ruedas tiene una bicicleta?", respuestas = new string[]{"1","2","3","4"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuánto es 6+2?", respuestas = new string[]{"7","8","9","10"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuánto es 5x2?", respuestas = new string[]{"8","9","10","12"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Qué mes tiene Navidad?", respuestas = new string[]{"Enero","Junio","Diciembre","Marzo"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Qué hora marca el mediodía?", respuestas = new string[]{"6 am","12 pm","6 pm","12 am"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué carta se pone roja al usarse?", respuestas = new string[]{"La usada","Ninguna","Todas","La moneda"}, indiceCorrecta = 0 },
        new Pregunta(){ pregunta = "¿Cuántos jugadores pelean?", respuestas = new string[]{"1","2","3","4"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Qué decide la moneda?", respuestas = new string[]{"Nada","Quién empieza","El color","El tiempo"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuántas casillas tiene cada lado?", respuestas = new string[]{"2","3","4","5"}, indiceCorrecta = 1 },
        new Pregunta(){ pregunta = "¿Cuántas cartas tiene cada jugador?", respuestas = new string[]{"3","4","5","6"}, indiceCorrecta = 2 },
        new Pregunta(){ pregunta = "¿Qué pasa si respondes bien el quiz?", respuestas = new string[]{"Nada","+10% habilidad","Pierdes","Se apaga"}, indiceCorrecta = 1 },
    };

    private GameObject panelQuiz;
    private TextMeshProUGUI textoPregunta;
    private Button[] botones = new Button[4];
    private RectTransform panelRect;

    private Peleador peleadorActual;
    private int indiceHabilidadActual;
    private Action<bool> callbackActual;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CrearPanel();
    }

    void CrearPanel()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        panelQuiz = new GameObject("PanelQuiz");
        panelQuiz.transform.SetParent(canvas.transform, false);
        Image fondo = panelQuiz.AddComponent<Image>();
        fondo.color = new Color(0.1f, 0.1f, 0.3f, 0.95f);
        panelRect = panelQuiz.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 350);

        Canvas cv = panelQuiz.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 200;
        panelQuiz.AddComponent<GraphicRaycaster>();

        textoPregunta = CrearTexto("Pregunta", panelQuiz.transform, "¿...?", new Vector2(0, 110), new Vector2(360, 80), 24);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            botones[i] = CrearBoton("Resp" + i, panelQuiz.transform, "R" + (i + 1), new Vector2(0, 30 - i * 60));
            botones[i].onClick.AddListener(() => Responder(idx));
        }

        panelQuiz.SetActive(false);
    }

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, Vector2 pos, Vector2 size, int fs)
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
        tmp.color = Color.white;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var f = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (f != null && f.Length > 0) tmp.font = f[0];
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
        rt.sizeDelta = new Vector2(340, 50);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.9f, 1f);
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
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var f = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (f != null && f.Length > 0) tmp.font = f[0];
            }
        }
        return btn;
    }

    public void MostrarQuiz(Peleador peleador, int indiceHabilidad, Action<bool> alResponder)
    {
        if (banco == null || banco.Count == 0) { alResponder?.Invoke(true); return; }

        peleadorActual = peleador;
        indiceHabilidadActual = indiceHabilidad;
        callbackActual = alResponder;

        Pregunta pr = banco[UnityEngine.Random.Range(0, banco.Count)];
        // Guardar pregunta actual para validar
        preguntaActual = pr;

        if (textoPregunta != null) textoPregunta.text = pr.pregunta;
        for (int i = 0; i < 4; i++)
        {
            if (botones[i] == null) continue;
            string txt = (pr.respuestas != null && i < pr.respuestas.Length) ? pr.respuestas[i] : "...";
            var tmp = botones[i].GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = (i + 1) + ") " + txt;
        }

        if (panelQuiz != null)
        {
            panelQuiz.SetActive(true);
            panelQuiz.transform.SetAsLastSibling();
            panelRect.anchoredPosition = Vector2.zero;
        }
    }

    private Pregunta preguntaActual;

    void Responder(int indice)
    {
        if (preguntaActual == null) return;
        bool correcta = (indice == preguntaActual.indiceCorrecta);
        Debug.Log(correcta ? "¡Correcta! +buff" : "Mal. -debuff");
        if (panelQuiz != null) panelQuiz.SetActive(false);
        callbackActual?.Invoke(correcta);
        callbackActual = null;
    }
}
