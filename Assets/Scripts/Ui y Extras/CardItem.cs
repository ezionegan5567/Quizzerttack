using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CardItem : MonoBehaviour
{
    private Button button;
    private Image cardImage;

    private void Awake()
    {
        button = GetComponent<Button>();
        cardImage = GetComponent<Image>();
    }

    private void Start()
    {
        button.onClick.AddListener(OnCardClicked);
    }

    private void OnCardClicked()
    {
        // Intentar agregar al mazo superior
        bool added = DeckManager.Instance.TryAddCard(this);

        if (added)
        {
            // Efecto estilo PvZ: la carta se deshabilita/apaga en la lista de abajo
            button.interactable = false;

            // Oscurecer o bajar la opacidad para marcar que ya fue elegida
            cardImage.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }
    }

    // Se llama cuando haces clic en la carta dentro del mazo para devolverla
    public void ReactivateCard()
    {
        button.interactable = true;
        cardImage.color = Color.white;
    }
}