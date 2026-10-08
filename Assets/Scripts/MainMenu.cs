using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string _gameSceneName;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private Button startButton;

    public void OnStartPressed()
    {
        startButton.interactable = false;
        StartCoroutine(FadeAndLoad());
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
}
