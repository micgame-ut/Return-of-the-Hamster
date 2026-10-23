using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Shows a "+points" popup above whatever earned them, using Score's
// OnPointsAdded event. Popups rise, fade, and are reused from a pool.
public class ScorePopups : MonoBehaviour
{
    [Tooltip("The popup's look: font, size, color, glow. Center alignment and a center pivot, Raycast Target off.")]
    [SerializeField] private TMP_Text popupPrefab;
    [Tooltip("Popups made at level start. More are made if needed.")]
    [SerializeField] private int prewarmCount = 5;
    [Tooltip("Canvas units above the earning body where a popup appears.")]
    [SerializeField] private float startOffset = 40f;
    [Tooltip("Canvas units it rises over its life.")]
    [SerializeField] private float riseDistance = 80f;
    [Tooltip("Seconds to rise and fade out.")]
    [SerializeField] private float lifetime = 0.8f;
    [Tooltip("Size for a tiny gain.")]
    [SerializeField] private float minScale = 1f;
    [Tooltip("Size at pointsForMaxScale and above.")]
    [SerializeField] private float maxScale = 2f;
    [Tooltip("The final gain (points times multiplier) that reaches maxScale.")]
    [SerializeField] private float pointsForMaxScale = 1000f;
    [Tooltip("The multiplier is added to the text (+325x2.0) only when it's above this.")]
    [SerializeField] private float showMultiplierAbove = 1.5f;

    [Header("Power-Up Labels")]
    [Tooltip("Shown when a Health power-up (or a Random one that rolled Health) is picked up. Empty = no popup.")]
    [SerializeField] private string healthLabel = "+1 LIFE";
    [Tooltip("Shown for Invincible.")]
    [SerializeField] private string invincibleLabel = "INVINCIBLE";
    [Tooltip("Shown for Twin.")]
    [SerializeField] private string twinLabel = "TWIN";
    [Tooltip("Shown for Score Multiplier.")]
    [SerializeField] private string scoreLabel = "SCORE x2";

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
        PowerUp.OnCollected += ShowPowerUp;
    }

    private void OnDisable()
    {
        if (score != null) score.OnPointsAdded -= Show;
        PowerUp.OnCollected -= ShowPowerUp;
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

    private void Show(float points, float multiplier, Vector3 worldPosition, string label)
    {
        int whole = Mathf.RoundToInt(points);
        string value = multiplier > showMultiplierAbove ? $"+{whole}x{multiplier:0.0}" : $"+{whole}";
        Spawn(string.IsNullOrEmpty(label) ? value : $"{label} {value}",
              Mathf.Lerp(minScale, maxScale, points * multiplier / pointsForMaxScale), worldPosition);
    }

    private void ShowPowerUp(PowerUp.PowerUpType type, Vector3 worldPosition)
    {
        string label = type switch
        {
            PowerUp.PowerUpType.Health => healthLabel,
            PowerUp.PowerUpType.Invincible => invincibleLabel,
            PowerUp.PowerUpType.Twin => twinLabel,
            PowerUp.PowerUpType.ScoreMultiplier => scoreLabel,
            _ => null,
        };
        if (!string.IsNullOrEmpty(label)) Spawn(label, minScale, worldPosition);
    }

    private void Spawn(string text, float scale, Vector3 worldPosition)
    {
        Popup popup = free.Count > 0 ? free.Pop() : CreatePopup();

        Vector2 screenPoint = cam.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 local);
        popup.start = local + Vector2.up * startOffset;
        popup.age = 0f;

        popup.text.text = text;
        popup.text.rectTransform.localScale = Vector3.one * scale;
        popup.text.rectTransform.localPosition = popup.start;
        popup.text.alpha = 1f;

        popup.text.gameObject.SetActive(true);
        active.Add(popup);
    }
}
