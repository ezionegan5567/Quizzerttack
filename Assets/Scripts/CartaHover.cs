using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class CartaHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Movimiento")]
    public float distanciaSubir = 100f;
    public float velocidad = 10f;

    [Header("UI Info de la Carta")]
    public GameObject panelInfo;
    public TextMeshProUGUI textoVida;
    public TextMeshProUGUI textoAtaque;
    public TextMeshProUGUI textoHabilidades;

    [Header("Datos de la Carta (se sincronizan con CartaSeleccion)")]
    public int vida = 100;
    public int ataque = 25;
    public string habilidades = "Golpe fuerte";

    private Vector3 posicionOriginal;
    private Vector3 posicionObjetivo;
    private bool mouseEncima = false;
    private CartaSeleccion seleccion;
    private UnityEngine.UI.Button boton;
    private int siblingOriginal;
    private Canvas panelCanvas;

    void Start()
    {
        posicionOriginal = transform.localPosition;
        posicionObjetivo = posicionOriginal;
        siblingOriginal = transform.GetSiblingIndex();
        seleccion = GetComponent<CartaSeleccion>();
        boton = GetComponent<UnityEngine.UI.Button>();

        if (panelInfo != null)
        {
            panelInfo.SetActive(false);
            // Orden superior para el panel de info
            panelCanvas = panelInfo.GetComponent<Canvas>();
            if (panelCanvas == null)
                panelCanvas = panelInfo.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 100;
        }
    }

    void Update()
    {
        if (seleccion != null && seleccion.EstaUsada()) return;
        transform.localPosition = Vector3.Lerp(transform.localPosition, posicionObjetivo, Time.deltaTime * velocidad);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (boton != null && !boton.interactable) return;
        if (seleccion != null && seleccion.EstaUsada()) return;

        mouseEncima = true;
        posicionObjetivo = posicionOriginal + Vector3.up * distanciaSubir;

        // Traer carta al frente
        transform.SetAsLastSibling();

        if (panelInfo != null)
        {
            panelInfo.SetActive(true);
            ActualizarInfo();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        mouseEncima = false;

        if (seleccion != null && seleccion.EstaUsada())
        {
            if (panelInfo != null)
                panelInfo.SetActive(false);
            return;
        }

        posicionObjetivo = posicionOriginal;
        transform.SetSiblingIndex(siblingOriginal);

        if (panelInfo != null)
            panelInfo.SetActive(false);
    }

    void ActualizarInfo()
    {
        if (seleccion != null)
        {
            if (textoVida != null) textoVida.text = "Vida: " + seleccion.vida;
            if (textoAtaque != null) textoAtaque.text = "Ataque: " + seleccion.ataque;
            if (textoHabilidades != null)
            {
                DatosPeleador d = DatosFacciones.Get(seleccion.nombreHeroe);
                if (d != null && d.habs != null)
                {
                    string[] ns = new string[d.habs.Length];
                    for (int i = 0; i < d.habs.Length; i++) ns[i] = d.habs[i].nombre;
                    textoHabilidades.text = "Habs: " + string.Join(" / ", ns);
                }
                else textoHabilidades.text = "Habilidades: " + habilidades;
            }
            return;
        }
        if (textoVida != null) textoVida.text = "Vida: " + vida;
        if (textoAtaque != null) textoAtaque.text = "Ataque: " + ataque;
        if (textoHabilidades != null) textoHabilidades.text = "Habilidades: " + habilidades;
    }
}
