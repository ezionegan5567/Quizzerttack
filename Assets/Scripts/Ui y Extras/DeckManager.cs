using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    [Header("Slots del Mazo (Los 6 espacios blancos)")]
    [SerializeField] private Image[] deckSlots = new Image[6]; // Asigna los 6 "Carta Seleccionada" aquí[cite: 4]

    [Header("Referencia al Carrusel")]
    [SerializeField] private Transform carruselContenedor; // Arrastra el objeto ContenedorCarrusel aquí

    // Mantiene el registro de qué carta está en cada slot
    private CardItem[] selectedCards = new CardItem[6];

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Limpiar slots al inicio y asignar clic a cada casilla superior
        for (int i = 0; i < deckSlots.Length; i++)
        {
            ClearSlotVisual(i);

            int slotIndex = i;
            Button slotBtn = deckSlots[i].GetComponent<Button>();
            if (slotBtn != null)
            {
                slotBtn.onClick.RemoveAllListeners();
                slotBtn.onClick.AddListener(() => RemoveCardFromDeck(slotIndex));
            }
        }
    }

    public bool TryAddCard(CardItem card)
    {
        // Buscar el primer slot vacío
        for (int i = 0; i < deckSlots.Length; i++)
        {
            if (selectedCards[i] == null)
            {
                selectedCards[i] = card;

                // Mostrar la imagen de la carta en el espacio blanco
                Image cardImage = card.GetComponent<Image>();
                if (cardImage != null)
                {
                    deckSlots[i].sprite = cardImage.sprite;
                    deckSlots[i].color = Color.white;
                }

                return true;
            }
        }

        Debug.Log("¡El mazo ya está lleno (6/6)!");
        return false;
    }

    public void RemoveCardFromDeck(int slotIndex)
    {
        if (selectedCards[slotIndex] != null)
        {
            // Reactivar la carta en el carrusel
            selectedCards[slotIndex].ReactivateCard();
            selectedCards[slotIndex] = null;

            // Limpiar la casilla blanca
            ClearSlotVisual(slotIndex);
        }
    }

    private void ClearSlotVisual(int index)
    {
        deckSlots[index].sprite = null;
        deckSlots[index].color = new Color(1f, 1f, 1f, 0.3f); // Vuelve semi-transparente la casilla
    }

    public void ClearAllDeck()
    {
        // 1. Limpiar los 6 slots superiores del mazo
        for (int i = 0; i < deckSlots.Length; i++)
        {
            selectedCards[i] = null;
            ClearSlotVisual(i);
        }

        // 2. Reactivar TODAS las cartas dentro del carrusel de abajo
        if (carruselContenedor != null)
        {
            foreach (Transform child in carruselContenedor)
            {
                CardItem item = child.GetComponent<CardItem>();
                if (item != null)
                {
                    item.ReactivateCard(); // Devuelve la interactividad y el color original
                }
            }
        }
    }

    // --- FUNCIÓN PARA EL BOTÓN MAZO ALEATORIO ---
    public void FillRandomDeck()
    {
        // 1. Limpia cualquier carta que estuviera en el mazo
        ClearAllDeck();

        if (carruselContenedor == null)
        {
            Debug.LogWarning("No asignaste el ContenedorCarrusel en el DeckManager");
            return;
        }

        // 2. Obtiene todas las cartas disponibles dentro del carrusel[cite: 5]
        List<CardItem> availableCards = new List<CardItem>();
        foreach (Transform child in carruselContenedor)
        {
            CardItem item = child.GetComponent<CardItem>();
            if (item != null)
            {
                availableCards.Add(item);
            }
        }

        if (availableCards.Count == 0) return;

        // 3. Mezcla la lista de cartas aleatoriamente (Algoritmo Fisher-Yates)
        for (int i = 0; i < availableCards.Count; i++)
        {
            CardItem temp = availableCards[i];
            int randomIndex = Random.Range(i, availableCards.Count);
            availableCards[i] = availableCards[randomIndex];
            availableCards[randomIndex] = temp;
        }

        // 4. Agrega hasta 6 cartas al mazo superior
        int cardsToPick = Mathf.Min(6, availableCards.Count);
        for (int i = 0; i < cardsToPick; i++)
        {
            CardItem card = availableCards[i];
            bool added = TryAddCard(card);

            if (added)
            {
                // Desactiva el botón y oscurece la carta original en el carrusel
                Button btn = card.GetComponent<Button>();
                if (btn != null) btn.interactable = false;

                Image img = card.GetComponent<Image>();
                if (img != null) img.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
        }
    }
}