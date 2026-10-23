using UnityEngine;

// On the ring of the player (and each twin copy): rolls the ring 360° on a side boost,
// easing like the boost itself, and shows the boost cooldown through the Ring Glow shader.
public class BoostRing : MonoBehaviour
{
    private static readonly int ChargeId = Shader.PropertyToID("_Charge");

    [Tooltip("The ring's local axis to roll around. Its sign sets which way the ring rolls for a given dodge. Flip to (0, 0, -1) to reverse.")]
    [SerializeField] private Vector3 spinAxis = Vector3.forward;

    private PlayerMovement movement;
    private Renderer ringRenderer;
    private MaterialPropertyBlock props;
    private float shownCharge = -1f; // last Charge sent to the shader; -1 so the first frame always sends it

    private Quaternion restRotation; // the ring's local rotation when not spinning
    private float spinTime;          // seconds since the current spin started
    private float spinDirection;     // +1 or -1, from the boost; 0 = not spinning

    private void Awake()
    {
        // On the player, PlayerMovement is on a parent. A twin copy has none, so it follows the player through GameManager.
        movement = GetComponentInParent<PlayerMovement>();
        if (movement == null && GameManager.Instance != null) movement = GameManager.Instance.Player;

        ringRenderer = GetComponent<Renderer>();
        props = new MaterialPropertyBlock();
        restRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (movement != null) movement.OnSideBoost += StartSpin;
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnSideBoost -= StartSpin;
    }

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

    // Sends the boost charge to the Ring Glow shader, only on frames where it changed.
    private void UpdateGlow()
    {
        if (movement == null || ringRenderer == null) return;

        float charge = movement.BoostCharge01;
        if (charge == shownCharge) return;

        shownCharge = charge;
        ringRenderer.GetPropertyBlock(props);
        props.SetFloat(ChargeId, charge);
        ringRenderer.SetPropertyBlock(props);
    }
}
