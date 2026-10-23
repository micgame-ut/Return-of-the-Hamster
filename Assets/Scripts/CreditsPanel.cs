using UnityEngine;
using System.Collections;
public class CreditsPanel : MonoBehaviour
{
    [SerializeField] private RectTransform _creditsContent;
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float scrollSpeed = 3f;
    [SerializeField] private float endPositionY;
    [SerializeField] private float pauseDuration = 3f;

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

        MusicManager.Instance.ReturnToTitle();   // stops credits, brings the title music back
    }
}
