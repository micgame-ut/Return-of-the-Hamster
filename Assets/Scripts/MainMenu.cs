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
    [Tooltip("The popup that asks whether to play the tutorial. Shown when Start is pressed. Needs a MenuNavigator with a Tutorial button and a Skip button. Empty = no popup, Start goes straight to the level.")]
    [SerializeField] private GameObject tutorialPopup;

    public static event System.Action OnStartClicked;
    public static event System.Action OnQuitClicked;

    private bool quitting;

    public void OnStartPressed()
    {
        startButton.interactable = false;
        OnStartClicked?.Invoke();
        if (tutorialPopup == null) BeginGame(false);
        else tutorialPopup.SetActive(true);
    }

    public void OnTutorialChosen() => BeginGame(true);
    public void OnTutorialSkipped() => BeginGame(false);

    private void BeginGame(bool withTutorial)
    {
        if (tutorialPopup != null) tutorialPopup.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        menuTransition.Play(withTutorial);
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
        if (quitting) return;
        quitting = true;
        OnQuitClicked?.Invoke();
        StartCoroutine(QuitAfterSound());
    }

    private IEnumerator QuitAfterSound()
    {
        yield return new WaitForSecondsRealtime(UISFX.QuitSoundLength);
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
        creditsButton.interactable = true;
        creditsPanel.OpenCredits();
    }

}
