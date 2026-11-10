using UnityEngine;
using System.Collections;
public class CreditsPanel : MonoBehaviour
{
    [Tooltip("The credits text that scrolls upward. Reset to its starting position each time the panel opens.")]
    [SerializeField] private RectTransform _creditsContent;
    [Tooltip("Seconds the credits sit still after opening, before they start scrolling.")]
    [SerializeField] private float holdDuration = 3f;
    [Tooltip("How fast the credits scroll upward, in canvas units per second.")]
    [SerializeField] private float scrollSpeed = 3f;
    [Tooltip("The Y position where scrolling stops.")]
    [SerializeField] private float endPositionY;
    [Tooltip("Seconds the credits stay on screen after reaching the end, before the panel hides.")]
    [SerializeField] private float pauseDuration = 3f;

    public static event System.Action OnOpened;
    public static event System.Action OnClosed;

    private Vector2 startPosition;

    private void Awake()
    {
        startPosition = _creditsContent.anchoredPosition;
    }

    public void OpenCredits()
    {
        gameObject.SetActive(true);
        _creditsContent.anchoredPosition = startPosition;
        StartCoroutine(PlayCredits());
        OnOpened?.Invoke();
    }

    private IEnumerator PlayCredits()
    {
        yield return new WaitForSeconds(holdDuration); // waits for the hold duration before auto-scrolls

        while (_creditsContent.anchoredPosition.y < endPositionY)
        {
            _creditsContent.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime; // scrolls the credits content upwards!
            yield return null; // waits for next frame
        }

        _creditsContent.anchoredPosition = new Vector2(_creditsContent.anchoredPosition.x, endPositionY); // this ensures the final position is set to the end position
        yield return new WaitForSeconds(pauseDuration); // waits for the pause duration before hiding the credits panel
        gameObject.SetActive(false); // hides the credits panel after the pause duration
        OnClosed?.Invoke();

        MusicManager.Instance.ReturnToTitle();   // stops credits, brings the title music back
    }
}
