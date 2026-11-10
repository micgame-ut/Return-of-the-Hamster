using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the end-of-level score breakdown: a total that starts at 0, then one row per
// source that earned points, each popping in and ticking the total up. Boost skips it.
public class ScoreBreakdown : MonoBehaviour
{
    [Tooltip("A label text and a value text, in that child order.")]
    [SerializeField] private GameObject rowPrefab;
    [Tooltip("One per Score.ScoreSource, in the same order.")]
    [SerializeField] private string[] labels = { "Progress", "Asteroids", "Close calls", "Waves", "Overdrive" };
    [Tooltip("Text shown before the total, for example SCORE 000123.")]
    [SerializeField] private string totalPrefix = "SCORE ";
    [Tooltip("Seconds between a row's label popping in and its value popping in.")]
    [SerializeField] private float segmentDelay = 0.12f;
    [Tooltip("How big a label or value snaps to when it pops in, before easing back to normal size.")]
    [SerializeField] private float popScale = 1.3f;
    [Tooltip("Seconds a pop takes to ease back to normal size.")]
    [SerializeField] private float popTime = 0.15f;
    [Tooltip("Seconds the total takes to tick up for each row.")]
    [SerializeField] private float countUpTime = 0.3f;
    [Tooltip("Seconds before the next row starts.")]
    [SerializeField] private float rowDelay = 0.4f;
    [Tooltip("Pressing this (the boost button) finishes the breakdown instantly.")]
    [SerializeField] private InputActionReference skipAction;

    public static event System.Action OnRowCounted;
    public static event System.Action OnFinished;

    private Score score;
    private bool skipping;

    private void Awake()
    {
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;
    }

    private void OnEnable()
    {
        if (skipAction != null) skipAction.action.Enable();
    }

    private void Update()
    {
        if (skipAction != null && skipAction.action.WasPressedThisFrame()) skipping = true;
    }

    // Waits in real time, ending early once the player skips.
    private IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (!skipping && Time.unscaledTime < end) yield return null;
    }

    // Shows a segment, snaps it to popScale and eases it back to normal size.
    private IEnumerator Pop(Transform segment)
    {
        segment.gameObject.SetActive(true);
        for (float t = 0f; t < popTime && !skipping; t += Time.unscaledDeltaTime)
        {
            segment.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, t / popTime);
            yield return null;
        }
        segment.localScale = Vector3.one;
    }

    // Adds one row, then pops its label in, then its value.
    private IEnumerator ShowRow(Transform rowContainer, string label, float points)
    {
        GameObject row = Instantiate(rowPrefab, rowContainer);
        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();
        texts[0].text = label;
        texts[1].text = Mathf.FloorToInt(points).ToString("N0");
        texts[0].gameObject.SetActive(false);
        texts[1].gameObject.SetActive(false);

        yield return Pop(texts[0].transform);
        yield return Wait(segmentDelay);
        yield return Pop(texts[1].transform);
    }

    // Ticks the total from one value to another over countUpTime.
    private IEnumerator CountUp(TMP_Text totalText, int from, int to)
    {
        for (float t = 0f; t < countUpTime && !skipping; t += Time.unscaledDeltaTime)
        {
            int shown = Mathf.RoundToInt(Mathf.Lerp(from, to, t / countUpTime));
            totalText.text = totalPrefix + shown.ToString("D6");
            yield return null;
        }
        totalText.text = totalPrefix + to.ToString("D6");
    }

    // Starts the breakdown from 0. EndScreen calls this when its screen opens.
    public void Play(TMP_Text totalText, Transform rowContainer)
    {
        skipping = false;
        StartCoroutine(Reveal(totalText, rowContainer));
    }

    // Shows the total at 0, then each source's row in order, ticking the total up after each.
    private IEnumerator Reveal(TMP_Text totalText, Transform rowContainer)
    {
        totalText.text = totalPrefix + 0.ToString("D6");
        if (score == null) yield break;

        float total = 0f;
        for (int i = 0; i < labels.Length; i++)
        {
            yield return Wait(rowDelay);

            float points = score.GetSourceTotal((Score.ScoreSource)i);
            if (points < 1f) continue;

            yield return ShowRow(rowContainer, labels[i], points);
            int from = Mathf.FloorToInt(total);
            total += points;
            OnRowCounted?.Invoke();
            yield return CountUp(totalText, from, Mathf.FloorToInt(total));
        }
        OnFinished?.Invoke();
    }
}
