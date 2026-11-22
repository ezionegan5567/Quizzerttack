using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardCarousel : MonoBehaviour
{
    [Header("Configuración del Carrusel")]
    [Tooltip("Distancia horizontal en pixeles entre cada carta (ancho de carta + espacio)")]
    [SerializeField] private float cardSpacing = 220f;

    [Tooltip("Velocidad de movimiento suave")]
    [SerializeField] private float lerpSpeed = 10f;

    private List<RectTransform> allCards = new List<RectTransform>();
    private InputSystem_Actions inputs;

    private void Awake()
    {
        inputs = new InputSystem_Actions();

        // Obtener todas las cartas hijas dentro de ContenedorCarrusel
        foreach (Transform child in transform)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                allCards.Add(rect);
            }
        }
    }

    private void OnEnable()
    {
        inputs.Player.Enable();
        inputs.Player.NextCard.performed += OnNextCardPerformed;
        inputs.Player.PreviousCard.performed += OnPreviousCardPerformed;
    }

    private void OnDisable()
    {
        if (inputs != null)
        {
            inputs.Player.NextCard.performed -= OnNextCardPerformed;
            inputs.Player.PreviousCard.performed -= OnPreviousCardPerformed;
            inputs.Player.Disable();
        }
    }

    private void Update()
    {
        if (allCards.Count == 0) return;

        // Offset para centrar el grupo de cartas respecto al punto origen del contenedor
        float offset = ((allCards.Count - 1) * cardSpacing) / 2f;

        for (int i = 0; i < allCards.Count; i++)
        {
            if (allCards[i] != null)
            {
                // Calcula la posición X objetivo para cada carta
                float targetX = (i * cardSpacing) - offset;
                Vector2 currentPos = allCards[i].anchoredPosition;

                // Anima suavemente la posición X
                float newX = Mathf.Lerp(currentPos.x, targetX, Time.deltaTime * lerpSpeed);
                allCards[i].anchoredPosition = new Vector2(newX, currentPos.y);
            }
        }
    }

    private void OnNextCardPerformed(InputAction.CallbackContext context) => NextCard();
    private void OnPreviousCardPerformed(InputAction.CallbackContext context) => PreviousCard();

    public void NextCard()
    {
        if (allCards.Count <= 1) return;

        // Mueve la primera carta al final de la lista
        RectTransform firstCard = allCards[0];
        allCards.RemoveAt(0);
        allCards.Add(firstCard);

        UpdateCardVisuals();
    }

    public void PreviousCard()
    {
        if (allCards.Count <= 1) return;

        // Mueve la última carta al principio de la lista
        RectTransform lastCard = allCards[allCards.Count - 1];
        allCards.RemoveAt(allCards.Count - 1);
        allCards.Insert(0, lastCard);

        UpdateCardVisuals();
    }

    private void UpdateCardVisuals()
    {
        // Actualiza el orden de renderizado en el Canvas para la profundidad
        for (int i = 0; i < allCards.Count; i++)
        {
            if (allCards[i] != null)
            {
                allCards[i].SetSiblingIndex(i);
            }
        }
    }
}