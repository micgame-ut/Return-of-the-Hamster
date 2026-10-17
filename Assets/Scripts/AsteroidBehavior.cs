using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Moves an asteroid toward the camera using one of several selectable
// movement patterns, chosen from a dropdown in the inspector. Before it
// starts moving, it blinks a warning glow so the player can see it coming.
public class AsteroidBehavior : MonoBehaviour
{
    public enum MovementType { Linear, Wave }
    public enum EffectType { None, Phase }

    [Header("Movement")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    [SerializeField] private float speed = 20f;

    [Header("Wave")]
    [SerializeField] private float amplitude = 30f;  // degrees swung each way around the orbit
    [SerializeField] private float frequency = 1f;   // cycles per second

    [Header("Effect")]
    [SerializeField] private EffectType effectType = EffectType.None;

    [Header("Phase")]
    [SerializeField] private float solidTime = 1.5f;        // seconds solid in each cycle
    [SerializeField] private float phasedTime = 1f;         // seconds phased (no collider, hologram look) in each cycle
    [SerializeField] private float flickerTime = 0.3f;      // seconds of flicker just before each switch
    [SerializeField] private float flickerInterval = 0.05f; // seconds between flicker flips

    [Header("Destruction")]
    [SerializeField] private bool destructible = false;
    [SerializeField] private float destroySpeed = 5f; // player Speed needed to break it (Speed ranges 0 to PlayerMovement.MaxSpeed)
    [SerializeField] private int scorePoints = 10; // base points when broken, before the speed multiplier

    [Header("Template")]
    [SerializeField] private SpawnableTemplate template;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    private Renderer rend;
    private Collider col;
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock outlineBlock;
    private WaitForSeconds blinkWait;
    private Transform mesh;
    private Transform cam;
    private PlayerInvincibility invincibility;
    private PlayerMovement player;
    private PlayerHealth health;
    private PlayerTwins twins;
    private Score score;
    private CloseCallZone closeCalls;
    private bool passedPlayer;      // has crossed the player's depth and been measured
    private bool closeCallJudged;   // the close call has been decided, so stop checking
    private bool hitPlayer;         // touched the player or a twin copy this flight
    private float closeCallGap;     // gap to the nearest body when it crossed
    private Vector3 closeCallBody;  // that body's position, for the popup
    private bool closeCallThreat;   // couldn't be broken when it crossed
    private int outlineIndex = -1; // material slot of the outline, -1 if none
    private int outlineState = -1; // 0 too slow, 1 can break, -1 not yet set
    private float hitRadius;            // collider footprint, measured once in Awake so it survives the collider being off
    private Material[] solidMaterials;  // the normal look, including any outline
    private Material[] phasedMaterials; // the hologram look
    private bool showingPhased;         // which look is on the renderer right now

    private Vector3 origin;
    private float elapsed;
    private float distance; // how far it has travelled toward the camera
    private bool isMoving;
    private Vector3 spinAxis;

    // Wave swings around the player's orbit, on the player's own circle
    private float baseAngle; // radians, where on the circle it was revealed

    // How fast this asteroid closes on the camera right now; rises with player speed.
    public float ApproachSpeed => speed + PlayerMovement.Speed * (template != null ? template.speedMultiplier : 0f);

    // Every revealed asteroid, so other scripts can react without searching the scene.
    public static readonly List<AsteroidBehavior> Active = new List<AsteroidBehavior>();

    public bool IsMoving => isMoving;
    public bool IsPhased { get; private set; }
    public bool CanBreak => !IsPhased &&
                            ((invincibility != null && invincibility.IsInvincible) ||
                             (destructible && PlayerMovement.Speed >= destroySpeed));

    // XY footprint of the collider, measured once in Awake, since its bounds are only valid while it's enabled.
    public float HitRadius => col != null ? hitRadius : 0f;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();

        // Measured now, while the collider is still on (WarnThenMove switches it off). The collider is on
        // the root, which never spins, so its footprint doesn't change.
        if (col != null) hitRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);

        block = new MaterialPropertyBlock();
        if (template != null) blinkWait = new WaitForSeconds(template.blinkInterval);

        // The renderer lives on the child mesh object, so that's what spins.
        mesh = rend != null ? rend.transform : null;
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
        invincibility = GameManager.Instance != null ? GameManager.Instance.PlayerInvincibility : null;
        player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        health = GameManager.Instance != null ? GameManager.Instance.PlayerHealth : null;
        twins = GameManager.Instance != null ? GameManager.Instance.PlayerTwins : null;
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;
        closeCalls = GameManager.Instance != null ? GameManager.Instance.CloseCallZone : null;

        // Destructible asteroids get the outline added as an extra material, which
        // renders the whole mesh a second time as the inverted hull. Done at runtime
        // so the Destructible toggle alone decides whether an asteroid is outlined.
        if (destructible && rend != null && template != null && template.outlineMaterial != null)
        {
            Material[] current = rend.sharedMaterials;
            Material[] withOutline = new Material[current.Length + 1];
            current.CopyTo(withOutline, 0);
            outlineIndex = current.Length;
            withOutline[outlineIndex] = template.outlineMaterial;
            rend.sharedMaterials = withOutline;
            outlineBlock = new MaterialPropertyBlock();
        }

        // The two looks a Phase asteroid swaps between, made once so a flicker flip allocates nothing.
        if (rend != null) solidMaterials = rend.sharedMaterials;
        if (effectType == EffectType.Phase && template != null && template.phasedMaterial != null)
            phasedMaterials = new[] { template.phasedMaterial };
    }

    private void OnEnable()
    {
        // Runs when the spawner activates this object, so each asteroid's
        // movement is measured from wherever it was revealed.
        Active.Add(this);
        origin = transform.position;
        elapsed = 0f;
        distance = 0f;
        isMoving = false;
        outlineState = -1; // force the outline color to refresh
        passedPlayer = false;
        closeCallJudged = false;
        hitPlayer = false;
        IsPhased = false;
        SetLook(false); // back to solid, in case it was despawned while phased

        // Each asteroid spins around its own random axis.
        spinAxis = Random.onUnitSphere;

        // The angle the asteroid sits at around the orbit center. Atan2 is the
        // reverse of cos/sin, turning an x/y back into an angle.
        Vector2 fromCenter = (Vector2)origin - OrbitSettings.Instance.OrbitCenter;
        baseAngle = Mathf.Atan2(fromCenter.y, fromCenter.x);

        StartCoroutine(WarnThenMove());
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private IEnumerator WarnThenMove()
    {
        if (template != null)
        {
            // No collisions while warning, so it can't hit the player before
            // it has visibly telegraphed itself.
            if (col != null) col.enabled = false;

            bool on = false;
            for (float t = 0f; t < template.warningTime; t += template.blinkInterval)
            {
                on = !on;
                SetGlow(on);
                yield return blinkWait;
            }

            SetGlow(false);
            if (col != null) col.enabled = true;
        }

        isMoving = true;
    }

    private void SetGlow(bool on)
    {
        if (rend == null || template == null) return;

        if (on)
        {
            // Property block overrides emission on this renderer only, without
            // creating a per-asteroid material copy. The material must have
            // Emission enabled for this to show.
            block.SetColor(EmissionColorId, template.GetGlow(movementType));
            rend.SetPropertyBlock(block);
        }
        else
        {
            rend.SetPropertyBlock(null); // back to the material's own emission
        }
    }

    // Swaps between the solid and hologram materials, only when the look actually changes.
    private void SetLook(bool phased)
    {
        if (phased == showingPhased || phasedMaterials == null) return;
        showingPhased = phased;
        rend.sharedMaterials = phased ? phasedMaterials : solidMaterials;
        if (!phased) outlineState = -1; // the outline slot is back, so refresh its color
    }

    // Phase effect: the collider follows the real cycle, and the look flickers just before each switch.
    private void UpdatePhase()
    {
        float cycle = solidTime + phasedTime;
        if (cycle <= 0f) return;

        float t = elapsed % cycle;
        IsPhased = t >= solidTime;
        if (col != null && col.enabled == IsPhased) col.enabled = !IsPhased;

        float untilSwitch = IsPhased ? cycle - t : solidTime - t;
        bool flip = untilSwitch <= flickerTime && flickerInterval > 0f &&
                    Mathf.FloorToInt(untilSwitch / flickerInterval) % 2 == 0;
        SetLook(IsPhased != flip);
    }

    private void Spin()
    {
        if (mesh == null || template == null) return;
        mesh.Rotate(spinAxis, template.spinSpeed * Time.deltaTime, Space.World);
    }

    private void UpdateOutline()
    {
        if (outlineIndex < 0 || showingPhased) return;

        // Only touch the renderer when the state flips, not every frame.
        int state = CanBreak ? 1 : 0;
        if (state == outlineState) return;
        outlineState = state;

        outlineBlock.SetColor(OutlineColorId, state == 1 ? template.outlineCanBreak : template.outlineTooSlow);
        rend.SetPropertyBlock(outlineBlock, outlineIndex);
    }

    private void Update()
    {
        // Spins during the warning too, so asteroids appear already tumbling.
        Spin();
        UpdateOutline();

        if (!isMoving) return;

        elapsed += Time.deltaTime;
        if (effectType == EffectType.Phase) UpdatePhase();

        // Approach speed rises with the player's speed, like the starfield, so
        // flying faster brings asteroids in faster. Distance is accumulated per
        // frame so a change in speed doesn't make the asteroid jump.
        distance += ApproachSpeed * Time.deltaTime;

        // Every type travels toward the camera (-Z).
        Vector3 position = origin + Vector3.back * distance;

        // Wave snaps onto the player's circle and swings back and forth along it.
        if (movementType == MovementType.Wave)
        {
            // Read every frame so it follows the orbit being tuned live
            OrbitSettings orbit = OrbitSettings.Instance;
            float swing = Mathf.Sin(elapsed * frequency * Mathf.PI * 2f) * amplitude * Mathf.Deg2Rad;
            float angle = baseAngle + swing;
            position.x = orbit.OrbitCenter.x + Mathf.Cos(angle) * orbit.OrbitRadius;
            position.y = orbit.OrbitCenter.y + Mathf.Sin(angle) * orbit.OrbitRadius;
        }

        transform.position = position;
        if (closeCalls != null && player != null && !closeCallJudged) TrackCloseCall();

        // Asteroids fly toward -Z, so once one is far enough behind the camera it
        // can never come back into view.
        if (cam != null && template != null &&
            transform.position.z < cam.position.z - template.despawnBehindCamera)
        {
            PatternCopy.Despawn(gameObject);
        }
    }

    // Measures the gap once, on the frame it crosses the player's depth, then judges
    // once it's too far past to still touch.
    private void TrackCloseCall()
    {
        float depth = transform.position.z - player.transform.position.z;
        if (!passedPlayer)
        {
            if (depth > 0f) return;
            passedPlayer = true;
            closeCallGap = closeCalls.NearestGap(transform.position, HitRadius, out closeCallBody);
            closeCallThreat = !CanBreak && !IsPhased;
        }

        if (depth > -(HitRadius + closeCalls.Reach)) return;
        closeCallJudged = true;

        if (!hitPlayer && closeCallThreat && closeCallGap > 0f && closeCallGap <= closeCalls.Distance && score != null)
            score.AddCloseCall(closeCallBody);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react to the player or a twin copy. FindBody also covers the
        // collider being on a child, and says which body was touched.
        if (player == null) return;
        Transform body = twins != null ? twins.FindBody(other.transform)
                       : other.transform.IsChildOf(player.transform) ? player.transform : null;
        if (body == null) return;
        hitPlayer = true;

        if (CanBreak)
        {
            Explode(body.position);
        }
        else if (twins != null && twins.IsTwinned)
        {
            twins.LoseBody(body);
        }
        else if (template != null)
        {
            // Push the player along their circle, away from the asteroid.
            // A dead-center hit picks a direction at random.
            float side = Vector2.Dot(player.transform.position - transform.position, player.OrbitTangent);
            float dir = side != 0f ? Mathf.Sign(side) : (Random.value < 0.5f ? -1f : 1f);
            if (player.TakeHit(dir * template.knockbackForce, template.speedLoss))
            {
                CameraShake.Shake(template.hitShakeDuration, template.hitShakeMagnitude);

                if (health != null) health.LoseLife();
            }
        }
    }

    private void Explode(Vector3 bodyPosition)
    {
        if (template != null)
        {
            EffectPool.Play(template.explosionPrefab, transform.position);

            CameraShake.Shake(template.shakeDuration, template.shakeMagnitude);
        }

        RunStats stats = GameManager.Instance != null ? GameManager.Instance.RunStats : null;
        if (stats != null) stats.AddAsteroidDestroyed();
        if (score != null) score.AddComboKill(scorePoints, bodyPosition);

        PatternCopy.Despawn(gameObject);
    }
}
