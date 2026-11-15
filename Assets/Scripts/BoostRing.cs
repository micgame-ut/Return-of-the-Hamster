using UnityEngine;

// On the ring of the player (and each twin copy): rolls the ring 360° on a side boost,
// easing like the boost itself, and shows the boost cooldown through the Ring Glow shader.
public class BoostRing : MonoBehaviour
{
    private static readonly int ChargeId = Shader.PropertyToID("_Charge");
    private static readonly int PulseId = Shader.PropertyToID("_Ready_Pulse");
    private static readonly int BaseColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int GlowColorId = Shader.PropertyToID("_Glow_Color");
    private static readonly int Ring1ColorId = Shader.PropertyToID("_Ring_1_Color");
    private static readonly int Ring2ColorId = Shader.PropertyToID("_Ring_2_Color");

    [Tooltip("The ring's local axis to roll around. Its sign sets which way the ring rolls for a given dodge. Flip to (0, 0, -1) to reverse.")]
    [SerializeField] private Vector3 spinAxis = Vector3.forward;

    [Tooltip("Seconds the ring's flash takes to fade out after the boost becomes ready. 0 = no flash.")]
    [SerializeField] private float readyPulseTime = 0.5f;

    [Header("Lance")]
    [Tooltip("The ring's base color while Lance is active (the normal one is a dark blue-gray).")]
    [SerializeField, ColorUsage(false, true)] private Color lanceBaseColor = new Color(0.3f, 0.05f, 0.2f);
    [Tooltip("The ring's glow color while Lance is active. HDR, so values above 1 shine with Bloom.")]
    [SerializeField, ColorUsage(false, true)] private Color lanceGlowColor = new Color(1f, 0.1f, 0.6f);
    [Tooltip("The color of the ring's first sharp line while Lance is active. HDR.")]
    [SerializeField, ColorUsage(false, true)] private Color lanceRing1Color = new Color(3f, 0.3f, 1.6f);
    [Tooltip("The color of the ring's second sharp line while Lance is active. HDR.")]
    [SerializeField, ColorUsage(false, true)] private Color lanceRing2Color = new Color(3f, 0.8f, 2.2f);

    private Color originalLanceBase, originalLanceGlow, originalLanceRing1, originalLanceRing2;   // the Inspector colours, kept so the original palette can put them back

    private PlayerMovement movement;
    private PlayerLance lance;
    private bool shownLance;         // whether the pink was last sent, so a change in either direction forces a resend
    private Renderer ringRenderer;
    private MaterialPropertyBlock props;
    private float shownCharge = -1f; // last Charge sent to the shader; -1 so the first frame always sends it
    private float shownPulse = -1f;  // last Ready Pulse sent to the shader; -1 so the first frame always sends it
    private float pulseTimer;        // seconds left on the ready flash; 0 = no flash

    private Quaternion restRotation; // the ring's local rotation when not spinning
    private float spinTime;          // seconds since the current spin started
    private float spinDirection;     // +1 or -1, from the boost; 0 = not spinning

    private void Awake()
    {
        // On the player, PlayerMovement is on a parent. A twin copy has none, so it follows the player through GameManager.
        movement = GetComponentInParent<PlayerMovement>();
        if (movement == null && GameManager.Instance != null) movement = GameManager.Instance.Player;
        lance = GameManager.Instance != null ? GameManager.Instance.PlayerLance : null;

        ringRenderer = GetComponent<Renderer>();
        props = new MaterialPropertyBlock();
        restRotation = transform.localRotation;

        originalLanceBase = lanceBaseColor;
        originalLanceGlow = lanceGlowColor;
        originalLanceRing1 = lanceRing1Color;
        originalLanceRing2 = lanceRing2Color;
    }

    private void OnEnable()
    {
        if (movement != null) movement.OnSideBoost += StartSpin;
        if (movement != null) movement.OnBoostReady += StartPulse;

        PaletteApplier.OnChanged += FollowPalette;
        FollowPalette(PaletteApplier.Current);
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnSideBoost -= StartSpin;
        if (movement != null) movement.OnBoostReady -= StartPulse;

        PaletteApplier.OnChanged -= FollowPalette;
    }

    // The four Lance colours follow the palette's Lance colour, each at its original strength.
    private void FollowPalette(ColorPalette palette)
    {
        if (palette == null || palette.isOriginal)
        {
            lanceBaseColor = originalLanceBase;
            lanceGlowColor = originalLanceGlow;
            lanceRing1Color = originalLanceRing1;
            lanceRing2Color = originalLanceRing2;
            return;
        }

        lanceBaseColor = ColorPalette.AtStrength(palette.lance, originalLanceBase.maxColorComponent);
        lanceGlowColor = ColorPalette.AtStrength(palette.lance, originalLanceGlow.maxColorComponent);
        lanceRing1Color = ColorPalette.AtStrength(palette.lance, originalLanceRing1.maxColorComponent);
        lanceRing2Color = ColorPalette.AtStrength(Color.Lerp(palette.lance, Color.white, 0.3f), originalLanceRing2.maxColorComponent);
    }

    private void StartPulse() => pulseTimer = readyPulseTime;

    private void StartSpin(float direction)
    {
        spinTime = 0f;
        spinDirection = direction;
    }

    private void Update()
    {
        UpdateSpin();
        UpdateGlow();
    }

    // Rolls the ring the same share of 360° as the dodge has covered of its distance, so both finish together.
    private void UpdateSpin()
    {
        if (spinDirection == 0f || movement == null) return;

        spinTime += Time.deltaTime;
        float share = 1f - Mathf.Exp(-movement.BoostDecay * spinTime);
        if (share >= 0.99f) share = 1f;

        transform.localRotation = restRotation * Quaternion.AngleAxis(share * 360f * spinDirection, spinAxis);
        if (share >= 1f) spinDirection = 0f;
    }

    // Sends the boost charge and the ready flash to the Ring Glow shader, only on frames where either changed.
    private void UpdateGlow()
    {
        if (movement == null || ringRenderer == null) return;

        float charge = movement.BoostCharge01;
        // The flash only belongs to a ready boost, so a boost during it cancels it.
        if (charge < 1f) pulseTimer = 0f;
        float pulse = 0f;
        if (pulseTimer > 0f)
        {
            pulseTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(pulseTimer / readyPulseTime);
            pulse = t * t; // squared, so it falls away fast after the first flash
        }
        bool lanceOn = lance != null && lance.IsActive;
        if (charge == shownCharge && pulse == shownPulse && lanceOn == shownLance) return;

        shownCharge = charge;
        shownPulse = pulse;
        shownLance = lanceOn;
        props.Clear();
        props.SetFloat(ChargeId, charge);
        props.SetFloat(PulseId, pulse);
        if (lanceOn) WriteLanceColors(props);
        ringRenderer.SetPropertyBlock(props);
    }

    // Writes the lance colors into block, so the small rings beside this ring can match it.
    public void WriteLanceColors(MaterialPropertyBlock block)
    {
        block.SetColor(BaseColorId, lanceBaseColor);
        block.SetColor(GlowColorId, lanceGlowColor);
        block.SetColor(Ring1ColorId, lanceRing1Color);
        block.SetColor(Ring2ColorId, lanceRing2Color);
    }
}
