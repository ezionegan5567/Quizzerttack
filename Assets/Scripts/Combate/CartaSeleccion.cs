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

    [Header("Visual auto del peleador")]
    public bool usarSpriteDelPrefab = true;
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
            // Usar imagen según el lado/jugador: izquierda usa set P1, derecha usa set P2
            if (art == null) art = GaleriaArte.Buscar(nombreHeroe, true, jugadorDueno);
            if (art == null) art = GaleriaArte.Buscar(nombreHeroe, false, jugadorDueno);
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
            colorOriginal = imagenCarta.color;
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
        {
            var c = colorOriginal;
            imagenCarta.color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, c.a);
        }
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
        imagenCarta.color = colorOriginal;
    }

    // ONLINE: re-lee el sprite para el nombre/facción nuevos (lo llama PanelRegistros).
    public void RefrescarSprite()
    {
        if (imagenCarta == null) imagenCarta = GetComponent<Image>();
        if (!usarSpriteDelPrefab || imagenCarta == null || prefabHeroe == null) return;
        Sprite art = imagenPeleador;
        // Usar imagen según el lado/jugador: izquierda usa set P1, derecha usa set P2
        if (art == null) art = GaleriaArte.Buscar(nombreHeroe, true, jugadorDueno);
        if (art == null) art = GaleriaArte.Buscar(nombreHeroe, false, jugadorDueno);
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
                // Player 1 (izquierda) usa su sprite de campo; Player 2 (derecha) el suyo.
                if (art == null) art = GaleriaArte.Buscar(nombreHeroe, true, jugadorDueno);
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
