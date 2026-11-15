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
    [Tooltip("Seconds a popup stays fully opaque before it starts to fade, so it can be read. Keep it below lifetime.")]
    [SerializeField] private float holdTime = 0.3f;
    [Tooltip("How much faster popups hold and fade for each popup showing beyond the first. 0.25 means 3 popups fade 1.5 times as fast. 0 turns it off.")]
    [SerializeField] private float crowdSpeedUp = 0.25f;
    [Tooltip("The most the hold and fade can speed up, as a multiple of normal speed. 1 turns it off.")]
    [SerializeField] private float maxSpeedUp = 3f;
    [Tooltip("Canvas units between stacked popups, times the popup's scale. A new popup kicks any popup it would overlap up, so they end this far above it.")]
    [SerializeField] private float stackSpacing = 50f;
    [Tooltip("How close sideways two popups must be to count as overlapping, in canvas units. Labels are wide, so keep it larger than a number's width.")]
    [SerializeField] private float stackWidth = 300f;
    [Tooltip("The widest a popup may be, as a share of the screen width. 0.5 = half the screen. A long popup is shrunk to fit, so it never runs off the screen.")]
    [SerializeField, Range(0.2f, 1f)] private float maxWidthShare = 0.5f;
    [Tooltip("Canvas units kept clear between a popup and the side of the screen. A popup near an edge is slid inward to stay clear.")]
    [SerializeField] private float edgeMargin = 40f;
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
    [Tooltip("Shown for Boost Recharge.")]
    [SerializeField] private string rechargeLabel = "RECHARGE";
    [Tooltip("Shown for Jackpot.")]
    [SerializeField] private string jackpotLabel = "JACKPOT";
    [Tooltip("Shown for Slim.")]
    [SerializeField] private string slimLabel = "SLIM";
    [Tooltip("Shown for Bomb.")]
    [SerializeField] private string bombLabel = "BOMB";

    [Header("Outline Looks")]
    [Tooltip("The look for plain points and every labeled bonus (CLOSE CALL, WAVE CLEAR, BONUS...): a TextMeshPro material with a black outline, made from the font's material.")]
    [SerializeField] private Material textLook;
    [Tooltip("The look for the +1 LIFE popup. Empty = textLook.")]
    [SerializeField] private Material healthLook;
    [Tooltip("The look for the INVINCIBLE popup. Empty = textLook.")]
    [SerializeField] private Material invincibleLook;
    [Tooltip("The look for the TWIN popup. Empty = textLook.")]
    [SerializeField] private Material twinLook;
    [Tooltip("The look for the SCORE x2 popup. Empty = textLook.")]
    [SerializeField] private Material scoreLook;
    [Tooltip("The look for the RECHARGE popup. Empty = textLook.")]
    [SerializeField] private Material rechargeLook;
    [Tooltip("The look for the JACKPOT popup. Empty = textLook.")]
    [SerializeField] private Material jackpotLook;
    [Tooltip("The look for the SLIM popup. Empty = textLook.")]
    [SerializeField] private Material slimLook;
    [Tooltip("The look for the BOMB popup. Empty = textLook.")]
    [SerializeField] private Material bombLook;

    private class Popup
    {
        public TMP_Text text;
        public Vector2 start;
        public float age;
        public float fadeAge;   // age for the hold and fade, which runs faster when crowded
        public float lift;   // extra height from being kicked up by newer popups
        public float scale;   // its size, used for the gap above it
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
        float speedUp = Mathf.Min(1f + crowdSpeedUp * Mathf.Max(active.Count - 1, 0), maxSpeedUp);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup popup = active[i];
            popup.age += Time.unscaledDeltaTime;
            popup.fadeAge += Time.unscaledDeltaTime * speedUp;
            float t = popup.age / lifetime;

            if (popup.fadeAge >= lifetime)
            {
                popup.text.gameObject.SetActive(false);
                active.RemoveAt(i);
                free.Push(popup);
                continue;
            }

            popup.text.rectTransform.localPosition = popup.start + Vector2.up * (riseDistance * t + popup.lift);
            // Fully opaque for holdTime, then fades out over the rest of lifetime. The Max stops a divide by zero if holdTime is set to lifetime or more.
            float fadeT = Mathf.Clamp01((popup.fadeAge - holdTime) / Mathf.Max(lifetime - holdTime, 0.01f));
            popup.text.alpha = 1f - fadeT;
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
              Mathf.Lerp(minScale, maxScale, points * multiplier / pointsForMaxScale), worldPosition, textLook);
    }

    private void ShowPowerUp(PowerUp.PowerUpType type, Vector3 worldPosition)
    {
        string label = type switch
        {
            PowerUp.PowerUpType.Health => healthLabel,
            PowerUp.PowerUpType.Invincible => invincibleLabel,
            PowerUp.PowerUpType.Twin => twinLabel,
            PowerUp.PowerUpType.ScoreMultiplier => scoreLabel,
            PowerUp.PowerUpType.BoostRecharge => rechargeLabel,
            PowerUp.PowerUpType.Jackpot => jackpotLabel,
            PowerUp.PowerUpType.Slim => slimLabel,
            PowerUp.PowerUpType.Bomb => bombLabel,
            _ => null,
        };
        Material look = type switch
        {
            PowerUp.PowerUpType.Health => healthLook,
            PowerUp.PowerUpType.Invincible => invincibleLook,
            PowerUp.PowerUpType.Twin => twinLook,
            PowerUp.PowerUpType.ScoreMultiplier => scoreLook,
            PowerUp.PowerUpType.BoostRecharge => rechargeLook,
            PowerUp.PowerUpType.Jackpot => jackpotLook,
            PowerUp.PowerUpType.Slim => slimLook,
            PowerUp.PowerUpType.Bomb => bombLook,
            _ => null,
        };
        if (!string.IsNullOrEmpty(label)) Spawn(label, minScale, worldPosition, look);
    }

    // Kicks every popup that sits where the new one will appear up, so the new one takes the spot and the older ones stack above it.
    private void KickUp(Vector2 start, float scale)
    {
        active.Sort((a, b) => a.text.rectTransform.localPosition.y.CompareTo(b.text.rectTransform.localPosition.y));
        float minY = start.y + stackSpacing * scale;
        foreach (Popup other in active)
        {
            Vector2 now = other.text.rectTransform.localPosition;
            if (Mathf.Abs(now.x - start.x) >= stackWidth) continue;
            if (now.y <= start.y - stackSpacing * other.scale) continue;
            if (now.y < minY)
            {
                other.lift += minY - now.y;
                now.y = minY;
            }
            minY = now.y + stackSpacing * other.scale;
        }
    }

    private void Spawn(string text, float scale, Vector3 worldPosition, Material look)
    {
        Popup popup = free.Count > 0 ? free.Pop() : CreatePopup();

        Material use = look != null ? look : textLook;   // an empty slot falls back to the black look
        if (use != null) popup.text.fontSharedMaterial = use;
        popup.text.text = text;
        float width = popup.text.preferredWidth;
        scale = Mathf.Min(scale, canvasRect.rect.width * maxWidthShare / Mathf.Max(width, 1f));   // a long label shrinks to fit

        Vector2 screenPoint = cam.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 local);
        popup.start = local + Vector2.up * startOffset;
        float reach = Mathf.Max(canvasRect.rect.width / 2f - edgeMargin - width * scale / 2f, 0f);   // how far from the middle its centre may sit
        popup.start.x = Mathf.Clamp(popup.start.x, -reach, reach);
        KickUp(popup.start, scale);
        popup.age = 0f;
        popup.fadeAge = 0f;
        popup.lift = 0f;
        popup.scale = scale;

        popup.text.rectTransform.localScale = Vector3.one * scale;
        popup.text.rectTransform.localPosition = popup.start;
        popup.text.alpha = 1f;

        popup.text.gameObject.SetActive(true);
        active.Add(popup);
    }
}
