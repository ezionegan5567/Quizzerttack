using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CartaSeleccion : MonoBehaviour, IPointerDownHandler
{
    [Header("Referencia de posiciones del jugador")]
    public Transform[] puntosLuchadores;

    [Header("Prefab del heroe")]
    public GameObject prefabHeroe;

    [Header("Info de la carta")]
    public string nombreHeroe = "Guerrero";
    public int vida = 100;
    public int ataque = 25;
    public int jugadorDueno = 1;
    public string faccion = "F1";

    [Header("Carta usada")]
    public Image imagenCarta;
    public Color colorOscurecida = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Visual auto del peleador")]
    public bool usarSpriteDelPrefab = true;
    public bool tenirPorFaccion = true;
    [Header("Arte del peleador (opcional: carta y campo)")]
    public Sprite imagenPeleador;
    public float tamanoPeleador = 1f;

    private bool seleccionada = false;
    private bool usada = false;
    private Color colorOriginal;
    private Vector3 posicionOriginal;
    private Vector3 posicionAbajo;
    private int siguientePosicion = 0;
    private GestorTurnos gestor;
    private Button boton;

    void Start()
    {
        posicionOriginal = transform.localPosition;
        posicionAbajo = posicionOriginal + Vector3.down * 50f;
        gestor = Object.FindFirstObjectByType<GestorTurnos>();
        boton = GetComponent<Button>();

        if (imagenCarta == null)
            imagenCarta = GetComponent<Image>();

        if (usarSpriteDelPrefab && imagenCarta != null && prefabHeroe != null)
        {
            Sprite art = imagenPeleador;
            if (art == null) art = GaleriaArte.Buscar(nombreHeroe);
            if (art == null)
            {
                SpriteRenderer sr = prefabHeroe.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) art = sr.sprite;
            }
            if (art != null)
            {
                imagenCarta.sprite = art;
                imagenCarta.preserveAspect = false;
            }
        }

        if (imagenCarta != null)
        {
            colorOriginal = tenirPorFaccion ? TinteFaccion(faccion) : imagenCarta.color;
            if (boton == null || boton.interactable)
                imagenCarta.color = colorOriginal;
        }
    }

    public static Color TinteFaccion(string f)
    {
        if (string.IsNullOrEmpty(f)) return Color.white;
        switch (f.Trim().ToUpper())
        {
            case "F1": return new Color(1f, 0.45f, 0.45f);
            case "F2": return new Color(0.45f, 0.65f, 1f);
            case "F3": return new Color(0.5f, 1f, 0.5f);
            case "F4": return new Color(1f, 0.9f, 0.45f);
            default: return Color.white;
        }
    }

    void Update()
    {
        if (seleccionada)
            transform.localPosition = Vector3.Lerp(transform.localPosition, posicionAbajo, Time.deltaTime * 15f);
        else
            transform.localPosition = Vector3.Lerp(transform.localPosition, posicionOriginal, Time.deltaTime * 15f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (usada) return;
        if (boton != null && !boton.interactable) return;
        if (gestor != null)
        {
            if (gestor.fase < 1 || gestor.fase > 2) return;
            if (gestor.GetTurnoActual() != jugadorDueno) return;
            if (!gestor.PuedeUsarCarta(jugadorDueno, faccion))
            {
                Debug.Log(nombreHeroe + " no es de tu facción.");
                return;
            }
        }

        seleccionada = !seleccionada;

        if (seleccionada)
        {
            bool spawneado = SpawnsHeroe();
            if (spawneado)
                MarcarComoUsada();
            else
                seleccionada = false;
        }
    }

    void MarcarComoUsada()
    {
        usada = true;
        seleccionada = true;
        if (imagenCarta != null)
            imagenCarta.color = colorOscurecida;
    }

    public void ReiniciarCarta()
    {
        usada = false;
        seleccionada = false;
        if (imagenCarta != null)
            imagenCarta.color = colorOriginal;
    }

    public void RefrescarVisual()
    {
        if (usada || imagenCarta == null) return;
        if (tenirPorFaccion) imagenCarta.color = TinteFaccion(faccion);
    }

    // ONLINE: re-lee el sprite para el nombre/facción nuevos (lo llama PanelRegistros).
    public void RefrescarSprite()
    {
        if (imagenCarta == null) imagenCarta = GetComponent<Image>();
        if (!usarSpriteDelPrefab || imagenCarta == null || prefabHeroe == null) return;
        Sprite art = imagenPeleador;
        if (art == null) art = GaleriaArte.Buscar(nombreHeroe);
        if (art == null)
        {
            SpriteRenderer sr = prefabHeroe.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) art = sr.sprite;
        }
        if (art != null)
        {
            imagenCarta.sprite = art;
            imagenCarta.preserveAspect = false;
        }
    }

    public bool EstaUsada()
    {
        return usada;
    }

    bool SpawnsHeroe()
    {
        if (prefabHeroe == null || puntosLuchadores == null) return false;

        for (int i = 0; i < puntosLuchadores.Length; i++)
        {
            Transform punto = puntosLuchadores[i];
            if (punto == null) continue;
            if (PuntoOcupado(punto)) continue;

            GameObject heroe = Instantiate(prefabHeroe, punto.position, Quaternion.identity, punto);
            heroe.name = nombreHeroe;

            SpriteRenderer heroSr = heroe.GetComponentInChildren<SpriteRenderer>();
            if (heroSr != null)
            {
                Sprite art = imagenPeleador;
                if (art == null) art = GaleriaArte.Buscar(nombreHeroe, true);
                if (art != null) heroSr.sprite = art;
                if (heroSr.sprite != null && heroSr.sprite.bounds.size.y > 0f)
                {
                    float s = tamanoPeleador / heroSr.sprite.bounds.size.y;
                    heroSr.transform.localScale = Vector3.one * s;
                }
            }

        Peleador p = heroe.GetComponent<Peleador>();
        if (p != null)
        {
            p.jugadorDueno = jugadorDueno;
            p.nombrePeleador = nombreHeroe;
            p.vida = vida;
            p.ataque = ataque;
            p.faccion = faccion;
            p.indiceCasilla = i;
        }

            return true;
        }

        Debug.Log("No hay mas posiciones disponibles");
        return false;
    }

    bool PuntoOcupado(Transform punto)
    {
        if (punto.childCount > 0) return true;
        Collider2D[] hits = Physics2D.OverlapCircleAll(punto.position, 0.4f);
        foreach (Collider2D h in hits)
        {
            if (h != null && h.GetComponent<Peleador>() != null)
                return true;
        }
        return false;
    }
}
