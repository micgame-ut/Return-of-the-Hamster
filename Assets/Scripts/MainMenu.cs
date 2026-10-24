using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
public class MainMenu : MonoBehaviour
{
    [Tooltip("The MenuTransition played when Start is pressed. Its duration sets how long the Start text blinks.")]
    [SerializeField] private MenuTransition menuTransition;
    [Tooltip("The Start button. Made non-interactive once pressed.")]
    [SerializeField] private Button startButton;
    [Tooltip("The Credits button. Made non-interactive once pressed.")]
    [SerializeField] private Button creditsButton;
    [Tooltip("The text on the Start button, which blinks when pressed.")]
    [SerializeField] private TMP_Text startButtonText;
    [Tooltip("The text on the Credits button, which blinks when pressed.")]
    [SerializeField] private TMP_Text creditsButtonText;
    [Tooltip("How many times the button text blinks per second after being pressed.")]
    [SerializeField] private float blinksPerSecond = 4f;
    [Tooltip("Seconds the Credits text blinks before the credits panel opens.")]
    [SerializeField] private float creditsBlinkDuration = 1f;
    [Tooltip("The CreditsPanel opened after the Credits blink.")]
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
