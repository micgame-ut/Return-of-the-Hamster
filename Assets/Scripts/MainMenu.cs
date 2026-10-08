using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using TMPro;
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string _gameSceneName;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private TMP_Text startButtonText;
    [SerializeField] private TMP_Text creditsButtonText;
    [SerializeField] private float blinksPerSecond = 4f;
    [SerializeField] private float creditsBlinkDuration = 1f;
    [SerializeField] private CreditsPanel creditsPanel;

    public void OnStartPressed()
    {
        startButton.interactable = false;
        StartCoroutine(FadeAndLoad());
        StartCoroutine(BlinkText(startButtonText, fadeDuration));
    }

    public void OnCreditsPressed()
    {
        creditsButton.interactable = false;
        StartCoroutine(BlinkText(creditsButtonText, creditsBlinkDuration));
        StartCoroutine(OpenCreditsAfterBlink());
    }

    public void OnOptionsPressed()
    {
        
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }

    private IEnumerator FadeAndLoad()
    {
        float elapsedTime = 0f; // starts timer
        while (elapsedTime < fadeDuration) 
        {
            elapsedTime += Time.deltaTime;
            fadePanel.alpha = elapsedTime / fadeDuration; 
            yield return null; // wait for next frame
        }
        fadePanel.alpha = 1f;
        SceneManager.LoadScene(_gameSceneName);
    }

    private IEnumerator BlinkText(TMP_Text text, float duration)
    {
        float interval = 1f / (blinksPerSecond * 2f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            text.enabled = !text.enabled;
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
        text.enabled = true;
    }

    private IEnumerator OpenCreditsAfterBlink()
    {
        yield return new WaitForSeconds(creditsBlinkDuration);
        creditsPanel.OpenCredits();
    }

}
