using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
public class MainMenu : MonoBehaviour
{
    [SerializeField] private MenuTransition menuTransition;
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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        menuTransition.Play();
        StartCoroutine(BlinkText(startButtonText, menuTransition.Duration));
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
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
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
