using UnityEngine;
using TMPro;

public class Temporizador : MonoBehaviour
{
    [Header("Configuracion del Temporizador")]
    public float tiempoInicial = 30f;
    public TextMeshProUGUI textoTemporizador;

    [Header("Cambio auto de fase")]
    public GestorTurnos gestor;

    private float tiempoRestante;
    private bool temporizadorActivo = false;

    void Start()
    {
        if (gestor == null)
            gestor = Object.FindFirstObjectByType<GestorTurnos>();
        tiempoRestante = tiempoInicial;
        temporizadorActivo = true;
        ActualizarTexto();
    }

    void Update()
    {
        if (temporizadorActivo && tiempoRestante > 0)
        {
            tiempoRestante -= Time.deltaTime;
            ActualizarTexto();

            if (tiempoRestante <= 0)
            {
                tiempoRestante = 0;
                temporizadorActivo = false;
                TiempoAgotado();
            }
        }
    }

    void ActualizarTexto()
    {
        int minutos = Mathf.FloorToInt(tiempoRestante / 60f);
        int segundos = Mathf.FloorToInt(tiempoRestante % 60f);
        textoTemporizador.text = $"tiempo: {minutos:00}:{segundos:00}";
    }

    void TiempoAgotado()
    {
        Debug.Log("Tiempo agotado! Cambiando de fase.");
        if (gestor == null)
            gestor = Object.FindFirstObjectByType<GestorTurnos>();
        if (gestor != null)
            gestor.PasarTurno();
        else
            IniciarTemporizador();
    }

    public void IniciarTemporizador()
    {
        tiempoRestante = tiempoInicial;
        temporizadorActivo = true;
    }

    public void PausarTemporizador()
    {
        temporizadorActivo = false;
    }

    public void ReanudarTemporizador()
    {
        temporizadorActivo = true;
    }

    public float ObtenerTiempoRestante()
    {
        return tiempoRestante;
    }
}
