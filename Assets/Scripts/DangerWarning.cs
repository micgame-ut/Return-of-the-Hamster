using UnityEngine;
using UnityEngine.UI;

// At 1 life, pulses red glows in from the left and right screen edges and
// blinks a warning sign in each corner.
public class DangerWarning : MonoBehaviour
{
    [SerializeField] private RectTransform warningPrefab;
    [SerializeField] private float cornerInset = 40f;
    [SerializeField, Min(0.01f)] private float blinkInterval = 0.4f;
    [SerializeField] private Color overlayColor = Color.red;
    [SerializeField] private float overlayAlphaMin = 0.4f;
    [SerializeField] private float overlayAlphaMax = 0.8f;
    [SerializeField] private float overlayWidthMin = 0.12f;
    [SerializeField] private float overlayWidthMax = 0.22f;

    private static readonly Vector2[] Corners =
    {
        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f)
    };

    private PlayerHealth health;
    private RectTransform canvasRect;
    private RectTransform[] warnings;
    private RawImage leftOverlay;
    private RawImage rightOverlay;
    private Texture2D gradient;
    private float blinkTimer;
    private bool active;
    private bool warningsOn;

    private void Awake()
    {
        GameManager gm = GameManager.Instance;
        health = gm != null ? gm.PlayerHealth : null;
        canvasRect = transform as RectTransform;

        gradient = CreateGradient();
        leftOverlay = CreateOverlay(true);
        rightOverlay = CreateOverlay(false);

        warnings = new RectTransform[warningPrefab != null ? Corners.Length : 0];
        for (int i = 0; i < warnings.Length; i++)
        {
            RectTransform warning = Instantiate(warningPrefab, canvasRect);
            warning.anchorMin = warning.anchorMax = warning.pivot = Corners[i];
            warning.anchoredPosition = new Vector2(
                Corners[i].x == 0f ? cornerInset : -cornerInset,
                Corners[i].y == 0f ? cornerInset : -cornerInset);
            warning.gameObject.SetActive(false);
            warnings[i] = warning;
        }
    }

    private void OnDestroy()
    {
        if (gradient != null) Destroy(gradient);
    }

    private void Start()
    {
        if (health != null) SetDanger(health.CurrentLives == 1);
    }

    private void OnEnable()
    {
        if (health != null) health.OnLivesChanged += HandleLivesChanged;
        if (GameManager.Instance != null) GameManager.Instance.OnWin += HandleWin;
    }

    private void OnDisable()
    {
        if (health != null) health.OnLivesChanged -= HandleLivesChanged;
        if (GameManager.Instance != null) GameManager.Instance.OnWin -= HandleWin;
        SetDanger(false);
    }

    private void HandleLivesChanged(int lives) => SetDanger(lives == 1);
    private void HandleWin() => SetDanger(false);

    private void Update()
    {
        if (!active) return;

        blinkTimer += Time.deltaTime;

        bool show = Mathf.FloorToInt(blinkTimer / blinkInterval) % 2 == 0;
        if (show != warningsOn) SetWarnings(show);

        float pulse = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * blinkTimer / blinkInterval);
        float width = canvasRect.rect.width * Mathf.Lerp(overlayWidthMin, overlayWidthMax, pulse);
        Color color = overlayColor;
        color.a = Mathf.Lerp(overlayAlphaMin, overlayAlphaMax, pulse);

        SetOverlayLook(leftOverlay, width, color);
        SetOverlayLook(rightOverlay, width, color);
    }

    private static void SetOverlayLook(RawImage overlay, float width, Color color)
    {
        if (overlay == null) return;
        overlay.rectTransform.sizeDelta = new Vector2(width, 0f);
        overlay.color = color;
    }

    private void SetDanger(bool on)
    {
        if (on == active) return;
        active = on;
        blinkTimer = 0f;
        SetWarnings(false);

        if (leftOverlay != null) leftOverlay.gameObject.SetActive(on);
        if (rightOverlay != null) rightOverlay.gameObject.SetActive(on);
    }

    private static Texture2D CreateGradient()
    {
        const int width = 256;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width];
        for (int x = 0; x < width; x++)
        {
            float t = x / (width - 1f);
            pixels[x] = new Color(1f, 1f, 1f, (1f - t) * (1f - t));
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private RawImage CreateOverlay(bool left)
    {
        GameObject go = new GameObject(left ? "Danger Glow Left" : "Danger Glow Right",
            typeof(RectTransform), typeof(RawImage));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(canvasRect, false);

        float side = left ? 0f : 1f;
        rect.anchorMin = new Vector2(side, 0f);
        rect.anchorMax = new Vector2(side, 1f);
        rect.pivot = new Vector2(side, 0.5f);
        rect.sizeDelta = Vector2.zero;

        RawImage image = go.GetComponent<RawImage>();
        image.texture = gradient;
        image.uvRect = left ? new Rect(0f, 0f, 1f, 1f) : new Rect(1f, 0f, -1f, 1f);
        image.raycastTarget = false;
        go.SetActive(false);
        return image;
    }

    private void SetWarnings(bool on)
    {
        warningsOn = on;
        for (int i = 0; i < warnings.Length; i++)
            warnings[i].gameObject.SetActive(on);
    }
}
