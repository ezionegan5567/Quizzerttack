using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardCarousel : MonoBehaviour
{
    [Header("Configuracion de Desplazamiento")]
    [Tooltip("Distancia en pixeles que se desplaza todo el mazo por cada paso")]
    public float stepSize = 130f;

    [Tooltip("Indice inicial (por ejemplo 8 para empezar centrado)")]
    public int currentIndex = 8;

    [Tooltip("Indice minimo (izquierda maxima)")]
    public int minIndex = 0;

    [Tooltip("Indice maximo (derecha maxima)")]
    public int maxIndex = 16;

    [Header("Cartas detectadas automaticamente")]
    [SerializeField] private List<RectTransform> allCards = new List<RectTransform>();

    private InputSystem_Actions inputs;
    private Vector2 initialContainerPosition;
    private RectTransform containerRect;

    private void Awake()
    {
        inputs = new InputSystem_Actions();

        containerRect = GetComponent<RectTransform>();
        if (containerRect != null)
        {
            // Guardamos la posicion base
            initialContainerPosition = containerRect.anchoredPosition;
        }

        foreach (Transform child in transform)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                allCards.Add(rect);
            }
        }
    }

    private void Start()
    {
        // Actualizamos la posicion inicial basada en el indice de arranque (ej. 8)
        UpdateCarouselPosition();
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
        MoveRight(); // D / Flecha Derecha
    }

    private void OnPreviousCardPerformed(InputAction.CallbackContext context)
    {
        MoveLeft(); // A / Flecha Izquierda
    }

    public void MoveRight()
    {
        if (currentIndex < maxIndex)
        {
            currentIndex++;
            UpdateCarouselPosition();
            Debug.Log("Mazo movido a la derecha. Indice actual: " + currentIndex);
        }
        else
        {
            Debug.Log("Llegaste al limite maximo de la derecha (" + maxIndex + ").");
        }
    }

    public void MoveLeft()
    {
        if (currentIndex > minIndex)
        {
            currentIndex--;
            UpdateCarouselPosition();
            Debug.Log("Mazo movido a la izquierda. Indice actual: " + currentIndex);
        }
        else
        {
            Debug.Log("Llegaste al limite minimo de la izquierda (" + minIndex + ").");
        }
    }

    private void UpdateCarouselPosition()
    {
        if (containerRect != null)
        {
            // Calculamos la posicion restando o sumando segun el desplazamiento respecto al inicio
            float targetX = initialContainerPosition.x - ((currentIndex - 8) * stepSize);
            containerRect.anchoredPosition = new Vector2(targetX, initialContainerPosition.y);
        }
    }
}