using UnityEngine;
using UnityEngine.UI;

public class BrightnessController : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Image brightnessOverlay;

    private void Start()
    {
        if (brightnessSlider != null && brightnessOverlay != null)
        {
            // Configurar el rango del slider de 0.2 (oscuro pero visible) a 1 (brillo máximo)
            brightnessSlider.minValue = 0.0f;
            brightnessSlider.maxValue = 1.0f;

            // Establecer el valor inicial basado en el estado actual del panel
            brightnessSlider.value = 1f - brightnessOverlay.color.a;

            // Escuchar los cambios del slider
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }
        else
        {
            Debug.LogError("Faltan referencias en el controlador de brillo.");
        }
    }

    public void OnBrightnessChanged(float sliderValue)
    {
        if (brightnessOverlay != null)
        {
            // El slider va de 0 (oscuro) a 1 (claro).
            // La opacidad (alpha) del panel negro es opuesta al brillo: 
            // Si el slider es 1, alpha es 0 (transparente/brillo máximo).
            // Si el slider es 0, alpha es 1 (negro total/sin brillo).
            Color overlayColor = brightnessOverlay.color;
            overlayColor.a = 1.0f - sliderValue;
            brightnessOverlay.color = overlayColor;
        }
    }
}