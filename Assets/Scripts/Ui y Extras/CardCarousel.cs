using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardCarousel : MonoBehaviour
{
    private List<RectTransform> allCards = new List<RectTransform>();
    private InputSystem_Actions inputs;

    private void Awake()
    {
        inputs = new InputSystem_Actions();

        // Detecta automaticamente las cartas hijas sin romper la serializacion de Unity
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

    private void OnNextCardPerformed(InputAction.CallbackContext context)
    {
        NextCard();
    }

    private void OnPreviousCardPerformed(InputAction.CallbackContext context)
    {
        PreviousCard();
    }

    public void NextCard()
    {
        if (allCards.Count <= 1) return;

        RectTransform firstCard = allCards[0];
        allCards.RemoveAt(0);
        allCards.Add(firstCard);

        UpdateCardVisuals();
        Debug.Log("Carrusel movido a la siguiente posicion.");
    }

    public void PreviousCard()
    {
        if (allCards.Count <= 1) return;

        RectTransform lastCard = allCards[allCards.Count - 1];
        allCards.RemoveAt(allCards.Count - 1);
        allCards.Insert(0, lastCard);

        UpdateCardVisuals();
        Debug.Log("Carrusel movido hacia atras.");
    }

    private void UpdateCardVisuals()
    {
        for (int i = 0; i < allCards.Count; i++)
        {
            if (allCards[i] != null)
            {
                allCards[i].SetSiblingIndex(i);
            }
        }
    }
}