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
    [SerializeField] private TMP_Text startButtonText;
    [SerializeField] private float blinksPerSecond = 4f;

    public void OnStartPressed()
    {
        startButton.interactable = false;
        StartCoroutine(FadeAndLoad());
    StartCoroutine(BlinkText());
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

    private IEnumerator BlinkText()
{
    float interval = 1f / (blinksPerSecond * 2f);
    float elapsed = 0f;
    while (elapsed < fadeDuration)
    {
        startButtonText.enabled = !startButtonText.enabled;
        yield return new WaitForSeconds(interval);
        elapsed += interval;
    }
    startButtonText.enabled = true;
}

}
