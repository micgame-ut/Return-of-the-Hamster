using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Shows a "+points" popup above whatever earned them, using Score's
// OnPointsAdded event. Popups rise, fade, and are reused from a pool.
public class ScorePopups : MonoBehaviour
{
    [SerializeField] private TMP_Text popupPrefab;
    [SerializeField] private int prewarmCount = 5;
    [SerializeField] private float startOffset = 40f;
    [SerializeField] private float riseDistance = 80f;
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float minScale = 1f;
    [SerializeField] private float maxScale = 2f;
    [SerializeField] private float pointsForMaxScale = 1000f;
    [SerializeField] private float showMultiplierAbove = 1.5f;

    private class Popup
    {
        public TMP_Text text;
        public Vector2 start;
        public float age;
    }

    private readonly Stack<Popup> free = new Stack<Popup>();
    private readonly List<Popup> active = new List<Popup>();
    private RectTransform canvasRect;
    private Camera cam;
    private Score score;

    private void Awake()
    {
        canvasRect = (RectTransform)transform;
        cam = GameManager.Instance.GameplayCamera;
        score = GameManager.Instance.Score;

        for (int i = 0; i < prewarmCount; i++)
            free.Push(CreatePopup());
    }

    private void OnEnable()
    {
        if (score != null) score.OnPointsAdded += Show;
    }

    private void OnDisable()
    {
        if (score != null) score.OnPointsAdded -= Show;
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup popup = active[i];
            popup.age += Time.unscaledDeltaTime;
            float t = popup.age / lifetime;

            if (t >= 1f)
            {
                popup.text.gameObject.SetActive(false);
                active.RemoveAt(i);
                free.Push(popup);
                continue;
            }

            popup.text.rectTransform.localPosition = popup.start + Vector2.up * (riseDistance * t);
            popup.text.alpha = 1f - t;
        }
    }

    private Popup CreatePopup()
    {
        TMP_Text text = Instantiate(popupPrefab, canvasRect);
        text.gameObject.SetActive(false);
        return new Popup { text = text };
    }

    private void Show(float points, float multiplier, Vector3 worldPosition)
    {
        Popup popup = free.Count > 0 ? free.Pop() : CreatePopup();

        Vector2 screenPoint = cam.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 local);
        popup.start = local + Vector2.up * startOffset;
        popup.age = 0f;

        int whole = Mathf.RoundToInt(points);
        popup.text.text = multiplier > showMultiplierAbove ? $"+{whole}x{multiplier:0.0}" : $"+{whole}";
        popup.text.rectTransform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, points * multiplier / pointsForMaxScale);
        popup.text.rectTransform.localPosition = popup.start;
        popup.text.alpha = 1f;

        popup.text.gameObject.SetActive(true);
        active.Add(popup);
    }
}
