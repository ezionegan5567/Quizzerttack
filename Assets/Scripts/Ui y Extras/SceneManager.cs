using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UGS_ON
using QuizzerAttack.Partida;
#endif


public class ScenesManager : MonoBehaviour
{
    /*public void MainPage()
    {
        SceneManager.LoadScene("OpeningMainScene");
    }

    public void MenuScene()
    {
        SceneManager.LoadScene("SelectScene");
    }

    public void SelectorScene()
    {
        SceneManager.LoadScene("SelectorScene");
    }

    public void Map1()
    {
        SceneManager.LoadScene("TestCinematic");
    }

    public void ModeScene()
    {
        SceneManager.LoadScene("ModeSelector");
    }

    public void CreditsScene()
    {
        SceneManager.LoadScene("CreditsScene");
    }

    public void Winning()
    {
        SceneManager.LoadScene("WinnerScene");
    }

    public void Quitgame()
    {
        Application.Quit();
    }*/

    [Header("Configuración de Transición")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float duration = 2f;

    private void Start()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(false);
        }
    }

    public void MainPage() => StartTransition("OpeningMainScene");
    public void SelectorScene() => StartTransition("SelectorScene");
    public void Map1() => StartTransition("TestCinematic");
    public void MazoScene()
    {
#if UGS_ON
        // El botón Jugar del menú principal es la entrada al PvP.
        // Si aún no hay identidad, abrir primero el acceso a cuenta.
        if (!ServiciosBootstrap.HaySesion)
        {
            StartTransition("Lab01-Autenticacion");
            return;
        }

        const string escenaBatalla = "campo de batalla";
        if (!Application.CanStreamedLevelBeLoaded(escenaBatalla))
        {
            Debug.LogError("La escena 'campo de batalla' no está habilitada en Build Settings.");
            return;
        }

        PanelPartidaOnline.PrepararEntradaDesdeMenu();
        StartTransition(escenaBatalla);
#else
        StartTransition("SeleccionaMazo");
#endif
    }
    public void OptionsScene() => StartTransition("EscenaOpciones");
    public void Winning() => StartTransition("WinnerScene");

    public void Quitgame()
    {
        StartCoroutine(QuitRoutine());
    }

    private void StartTransition(string sceneName)
    {
        StartCoroutine(FadeAndLoadRoutine(sceneName));
    }

    private IEnumerator FadeAndLoadRoutine(string sceneName)
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            float timer = 0f;
            Color originalColor = fadeImage.color;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;

                originalColor.a = Mathf.Lerp(0f, 1f, progress);
                fadeImage.color = originalColor;

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator QuitRoutine()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            float timer = 0f;
            Color originalColor = fadeImage.color;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                originalColor.a = Mathf.Lerp(0f, 1f, timer / duration);
                fadeImage.color = originalColor;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        Application.Quit();
    }
}
