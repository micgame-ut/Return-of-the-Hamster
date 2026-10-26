using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Moves an asteroid toward the camera using one of several selectable
// movement patterns, chosen from a dropdown in the inspector. Before it
// starts moving, it blinks a warning glow so the player can see it coming.
public class AsteroidBehavior : MonoBehaviour
{
    public enum MovementType { Linear, Wave, Spiral, Homing, StopAndGo }
    public enum EffectType { None, Phase, Splitter, Growing, Magnet, Frost }
    public enum AsteroidKind { Hazard, Destructible, BonusCarrier }

    [Header("Kind")]
    [Tooltip("What it is: Hazard hurts and can't be smashed, Destructible and BonusCarrier can be smashed by speed (BonusCarrier is worth more).")]
    [SerializeField] private AsteroidKind kind = AsteroidKind.Hazard;

    [Header("Movement")]
    [Tooltip("The path it takes: Linear, Wave, Spiral, Homing or StopAndGo.")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    [Tooltip("Multiplies this asteroid's approach speed. 1 = normal, 0.3 = a slow drifter.")]
    [SerializeField] private float approachScale = 1f;

    [Header("Wave")]
    [Tooltip("Wave: how many degrees it swings each way around the player's circle. Unused by Linear.")]
    [SerializeField] private float amplitude = 30f;
    [Tooltip("Wave: full back-and-forth swings per second. Unused by Linear.")]
    [SerializeField] private float frequency = 1f;

    [Header("Spiral")]
    [Tooltip("Spiral: degrees per second it drifts round the orbit. Negative = the other way. Unused by Linear and Wave.")]
    [SerializeField] private float spiralSpeed = 45f;

    [Header("Homing")]
    [Tooltip("Homing: the fastest it can turn toward the nearest player body, in degrees per second. Unused by the other movements.")]
    [SerializeField] private float homingSpeed = 40f;
    [Tooltip("Homing: how close to the player's depth it stops turning and flies straight, in units, so the last stretch can be dodged.")]
    [SerializeField] private float homingStopDistance = 8f;
    [Tooltip("Homing: the clear space it keeps from other asteroids, as a fraction of a collider's width. 1 = a gap as wide as a collider, 0 = it only stops them touching. A Homing asteroid holds still rather than turn into one.")]
    [SerializeField] private float homingGapScale = 1f;

    [Header("Stop And Go")]
    [Tooltip("StopAndGo: how far ahead of the player's depth it ends up stopped, in units.")]
    [SerializeField] private float stopDistance = 30f;
    [Tooltip("StopAndGo: it starts slowing this many units before stopDistance, so the stop is smooth. 0 = an abrupt stop.")]
    [SerializeField] private float stopEaseDistance = 10f;
    [Tooltip("StopAndGo: seconds it hangs still before the dash, including the shake at the end.")]
    [SerializeField] private float stopTime = 0.75f;
    [Tooltip("StopAndGo: seconds at the end of the stop when it shakes, as a warning.")]
    [SerializeField] private float shakeTime = 0.3f;
    [Tooltip("StopAndGo: how far the shake moves it, in units.")]
    [SerializeField] private float shakeMagnitude = 0.15f;
    [Tooltip("StopAndGo: the dash speed as a multiple of its normal approach speed.")]
    [SerializeField] private float dashMultiplier = 2.5f;

    [Header("Effect")]
    [Tooltip("Extra behavior on top of the movement: None, Phase, Splitter, Growing, Magnet or Frost. A separate dropdown from movementType.")]
    [SerializeField] private EffectType effectType = EffectType.None;

    [Header("Phase")]
    [Tooltip("Phase: seconds solid in each cycle.")]
    [SerializeField] private float solidTime = 1.5f;
    [Tooltip("Phase: seconds phased (collider off, hologram look) in each cycle.")]
    [SerializeField] private float phasedTime = 1f;
    [Tooltip("Phase: seconds of flicker just before each switch, as a warning.")]
    [SerializeField] private float flickerTime = 0.3f;
    [Tooltip("Phase: seconds between flicker flips. 0 = no flicker.")]
    [SerializeField] private float flickerInterval = 0.05f;

    [Header("Splitter")]
    [Tooltip("Splitter: how far ahead of the player's depth it splits into pieces, in units.")]
    [SerializeField] private float splitDistance = 40f;
    [Tooltip("Splitter: how many pieces it splits into.")]
    [SerializeField] private int splitCount = 3;
    [Tooltip("Splitter: degrees between neighbouring pieces round the orbit.")]
    [SerializeField] private float splitSpread = 20f;
    [Tooltip("Splitter: the asteroid prefab each piece is made from (a Linear one). Its Meshes list gives each piece a random mesh.")]
    [SerializeField] private GameObject splitPiece;
    [Tooltip("Splitter: how much smaller than the original each piece is. 0.5 = half size.")]
    [SerializeField] private float pieceScale = 0.5f;

    [Header("Growing")]
    [Tooltip("Growing: how far ahead of the player's depth it pops to its full size, in units.")]
    [SerializeField] private float growDistance = 30f;
    [Tooltip("Growing: its final size, as a multiple of its starting size. 2 = double.")]
    [SerializeField] private float growScale = 2f;
    [Tooltip("Growing: it starts pulsing this many units before it pops, as a warning.")]
    [SerializeField] private float pulseDistance = 10f;
    [Tooltip("Growing: how much the mesh swells and shrinks while pulsing, as a fraction of its size. 0.1 = 10% either way.")]
    [SerializeField] private float pulseSize = 0.1f;
    [Tooltip("Growing: pulses per second.")]
    [SerializeField] private float pulseRate = 6f;
    [Tooltip("Growing: how far past its final size the pop goes, as a fraction. 0.25 = 25% bigger for an instant.")]
    [SerializeField] private float overshoot = 0.25f;
    [Tooltip("Growing: seconds the overshoot takes to ease back down to the final size.")]
    [SerializeField] private float settleTime = 0.2f;

    [Header("Magnet")]
    [Tooltip("Magnet: how close the player has to be to feel the pull, in units (3D, including depth). The pull grows the closer it gets.")]
    [SerializeField] private float magnetRadius = 30f;
    [Tooltip("Magnet: the pull on the player at point-blank range, in degrees per second along the orbit.")]
    [SerializeField] private float magnetStrength = 60f;

    [Header("Frost")]
    [Tooltip("Frost: how far the comet has to move round the orbit ring before it leaves another patch, in units. Keep it at or below twice patchRadius so the trail has no gaps.")]
    [SerializeField] private float dropSpacing = 3f;
    [Tooltip("Frost: it only leaves patches while it is within this distance of the player's depth, before and after crossing it, in units.")]
    [SerializeField] private float paintDistance = 20f;
    [Tooltip("Frost: seconds a patch lasts before it disappears. The ribbon and the mist last this long too.")]
    [SerializeField] private float patchLifetime = 6f;
    [Tooltip("Frost: how close the player has to be to a patch's centre to be slowed, in units. Patches sit on the orbit ring.")]
    [SerializeField] private float patchRadius = 3f;
    [Tooltip("Frost: the fraction of the player's forward Speed lost per second inside a patch. 0 = none, 1 = all of it each second.")]
    [SerializeField, Range(0f, 1f)] private float speedSlow = 0.5f;
    [Tooltip("Frost: the fraction of the player's sideways movement along the orbit lost inside a patch. 0 = none, 1 = frozen in place.")]
    [SerializeField, Range(0f, 1f)] private float orbitSlow = 0.5f;
    [Tooltip("Frost: the particle system that draws the mist, a child of the comet. Its Start Lifetime is set to patchLifetime when the comet spawns, so the mist lasts as long as a patch. Leave empty for no mist.")]
    [SerializeField] private ParticleSystem trail;
    [Tooltip("Frost: the Trail Renderer that draws the ribbon, a child of the comet. Its Time is set to patchLifetime when the comet spawns, so the ribbon lasts as long as a patch. Leave empty for no ribbon.")]
    [SerializeField] private TrailRenderer ribbon;

    [Header("Destruction")]
    [Tooltip("The player Speed needed to smash it (compare to maxSpeed, 10 by default).")]
    [SerializeField] private float destroySpeed = 5f;
    [Tooltip("Base points for smashing it, before Score's combo bonus and speed multiplier. Set per prefab so each asteroid type can be worth a different amount.")]
    [SerializeField] private int scorePoints = 10;

    [Header("Mesh")]
    [Tooltip("Models to pick from at random. Empty keeps the prefab's own mesh.")]
    [SerializeField] private Mesh[] meshes;

    [Header("Template")]
    [Tooltip("The shared SpawnableTemplate asset.")]
    [SerializeField] private SpawnableTemplate template;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintId = Shader.PropertyToID("_Tint");   // the Asteroid Phase graph's tint property

    private Renderer rend;
    private MeshFilter meshFilter;
    private Collider col;
    private MaterialPropertyBlock outlineBlock;
    private MaterialPropertyBlock tintBlock;
    private Color[] baseColors;  // each non-outline slot's own base color, before the depth tint
    private Color lastTint;  // the tint last sent to the renderer, so unchanged frames skip the call
    private WaitForSeconds warnWait;
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
    private float startRadius; // its distance from the orbit center, measured when it's revealed
    private float homingAngle;  // radians, where a Homing asteroid is on the circle right now
    private bool homingLocked;  // it has come close enough to stop turning

    // StopAndGo goes through these in order, once each.
    private enum StopPhase { Approaching, Stopped, Dashing }
    private StopPhase stopPhase;
    private float stopTimer;  // seconds it has hung still so far
    private Vector3 meshHome;  // where the mesh child normally sits, so a shake can return to it
    private bool isPiece;  // a Splitter's piece: it moves at once, with no warning wait
    private float fullHitRadius;  // hitRadius at the prefab's own size, so a piece can scale it
    private bool hasSplit;  // a Splitter has already made its pieces
    private bool hasGrown;  // a Growing asteroid has popped to its final size
    private float settleTimer;  // seconds since the pop, for the overshoot
    private Vector3 startScale;  // the root's scale at the start, put back when a Growing asteroid is reused
    private Vector3 meshStartScale;  // the mesh child's scale at the start, for pulsing

    // Frost: the trail is a list of these. A struct is a small value stored directly in the list, with no object per patch.
    private struct FrostPatch { public Vector3 position; public float age; }
    private readonly List<FrostPatch> patches = new List<FrostPatch>();
    private Vector3 lastDrop;  // where the last patch was left
    private bool hasDropped;  // a patch has been left this flight, so lastDrop means something
    private bool retired;  // a Frost comet that has finished flying and is only waiting for its trail to run out

    // How fast this asteroid closes on the camera right now; rises with player speed.
    public float ApproachSpeed => template != null ? (template.baseApproachSpeed + PlayerMovement.Speed * template.speedMultiplier) * approachScale : 0f;

    // Every revealed asteroid, so other scripts can react without searching the scene.
    public static readonly List<AsteroidBehavior> Active = new List<AsteroidBehavior>();

    public bool IsMoving => isMoving;
    public MovementType Movement => movementType;
    public float Amplitude => amplitude;
    public float Frequency => frequency;
    // Set by the spawner when it flips the pattern left-right: a Wave asteroid then swings the other way.
    public bool MirrorSwing { get; set; }
    // Set by EffectPool on a pooled piece: the prefab it was made from, so it knows which pile to go back to.
    public GameObject PoolSource { get; set; }
    public bool IsPhased { get; private set; }
    // Whether the player's speed can smash this kind. Hazard can't.
    private bool BreaksBySpeed => kind == AsteroidKind.Destructible || kind == AsteroidKind.BonusCarrier;
    public bool CanBreak => !IsPhased &&
                            ((invincibility != null && invincibility.IsInvincible) ||
                             (BreaksBySpeed && PlayerMovement.Speed >= destroySpeed));

    // XY footprint of the collider, measured once in Awake, since its bounds are only valid while it's enabled.
    public float HitRadius => col != null ? hitRadius : 0f;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter != null && meshes.Length > 0)
        {
            Mesh picked = meshes[Random.Range(0, meshes.Length)];
            if (picked != null) meshFilter.sharedMesh = picked;
        }
        col = GetComponent<Collider>();

        // Measured now, while the collider is still on (WarnThenMove switches it off). The collider is on
        // the root, which never spins, so its footprint doesn't change.
        if (col != null) hitRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
        fullHitRadius = hitRadius;

        if (template != null) warnWait = new WaitForSeconds(template.warningTime);

        // The renderer lives on the child mesh object, so that's what spins.
        mesh = rend != null ? rend.transform : null;
        if (mesh != null) meshHome = mesh.localPosition;
        startScale = transform.localScale;
        if (mesh != null) meshStartScale = mesh.localScale;
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
        invincibility = GameManager.Instance != null ? GameManager.Instance.PlayerInvincibility : null;
        player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        health = GameManager.Instance != null ? GameManager.Instance.PlayerHealth : null;
        twins = GameManager.Instance != null ? GameManager.Instance.PlayerTwins : null;
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;
        closeCalls = GameManager.Instance != null ? GameManager.Instance.CloseCallZone : null;

        // Asteroids that speed can smash get the outline added as an extra material, which
        // renders the whole mesh a second time as the inverted hull. Done at runtime
        // so the Kind alone decides whether an asteroid is outlined.
        if (BreaksBySpeed && rend != null && template != null && template.outlineMaterial != null)
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

        // Each slot's own base color, kept for the depth tint. The outline slot is left out so it stays bright.
        if (rend != null)
        {
            tintBlock = new MaterialPropertyBlock();
            int slots = outlineIndex >= 0 ? outlineIndex : solidMaterials.Length;
            baseColors = new Color[slots];
            for (int i = 0; i < slots; i++)
                baseColors[i] = solidMaterials[i] != null && solidMaterials[i].HasProperty(BaseColorId)
                    ? solidMaterials[i].GetColor(BaseColorId)
                    : Color.white;
        }
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
        startRadius = fromCenter.magnitude;
        homingAngle = baseAngle;
        homingLocked = false;
        stopPhase = StopPhase.Approaching;
        stopTimer = 0f;
        hasSplit = false;
        patches.Clear();
        hasDropped = false;
        retired = false;
        if (rend != null) rend.enabled = true;
        if (effectType == EffectType.Frost)
        {
            if (ribbon != null) ribbon.time = patchLifetime;
            if (trail != null)
            {
                ParticleSystem.MainModule main = trail.main;
                main.startLifetime = patchLifetime;
            }
        }
        if (effectType == EffectType.Splitter && splitPiece != null)
            EffectPool.PrewarmAsteroids(splitPiece, splitCount);
        hasGrown = false;
        settleTimer = 0f;
        if (effectType == EffectType.Growing)
        {
            transform.localScale = startScale;
            if (mesh != null) mesh.localScale = meshStartScale;
            hitRadius = fullHitRadius;
        }

        StartCoroutine(WarnThenMove());
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    // Called on a piece while it's still switched off, by whatever makes it: shrinks it and has it skip the warning wait.
    public void BecomePiece(float scale)
    {
        isPiece = true;
        Vector3 fullScale = PoolSource != null ? PoolSource.transform.localScale : transform.localScale;
        transform.localScale = fullScale * scale;
        hitRadius = fullHitRadius * scale;
        if (col != null) col.enabled = true;
    }

    private IEnumerator WarnThenMove()
    {
        if (template != null && !isPiece)
        {
            // No collisions while warning, so it can't hit the player before
            // it has visibly telegraphed itself.
            if (col != null) col.enabled = false;

            yield return warnWait;
            if (col != null) col.enabled = true;
        }

        isMoving = true;
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

    // Fades the body toward template.farColor the further it is ahead of the player. The outline slot is left alone.
    private void UpdateDepthTint()
    {
        if (baseColors == null || template == null || player == null) return;

        Color tint = template.GetDepthTint(transform.position.z - player.transform.position.z);
        if (tint == lastTint) return;
        lastTint = tint;

        for (int i = 0; i < baseColors.Length; i++)
        {
            tintBlock.SetColor(BaseColorId, baseColors[i] * tint);
            tintBlock.SetColor(TintId, tint);
            rend.SetPropertyBlock(tintBlock, i);
        }
    }

    // The angle (radians) round the orbit of the player body, or twin copy, nearest to this asteroid.
    private float ClosestBodyAngle()
    {
        Vector3 body = player.transform.position;
        if (closeCalls != null) closeCalls.NearestGap(transform.position, 0f, out body);

        Vector2 fromCenter = (Vector2)body - OrbitSettings.Instance.OrbitCenter;
        return Mathf.Atan2(fromCenter.y, fromCenter.x);
    }

    // True if moving to newPosition would bring this asteroid closer to another one than its gap allows.
    private bool WouldCrowd(Vector3 newPosition)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            AsteroidBehavior other = Active[i];
            if (other == this || other.IsPhased) continue;

            float required = (HitRadius + other.HitRadius) * (1f + homingGapScale);
            float newDistance = Vector3.Distance(newPosition, other.transform.position);
            if (newDistance < required && newDistance < Vector3.Distance(transform.position, other.transform.position))
                return true;
        }
        return false;
    }

    // StopAndGo: moves through its phases and returns how fast to approach this frame, as a multiple of normal.
    private float StopAndGoFactor()
    {
        if (player == null) return 1f;

        if (stopPhase == StopPhase.Approaching)
        {
            float remaining = transform.position.z - player.transform.position.z - stopDistance;
            if (remaining > 0f)
                return stopEaseDistance > 0f ? Mathf.Sqrt(Mathf.Min(remaining / stopEaseDistance, 1f)) : 1f;
            stopPhase = StopPhase.Stopped;
        }

        if (stopPhase == StopPhase.Stopped)
        {
            stopTimer += Time.deltaTime;
            if (stopTimer < stopTime) return 0f;
            stopPhase = StopPhase.Dashing;
        }

        return dashMultiplier;
    }

    // StopAndGo: shakes the mesh for the last shakeTime of the stop, as a warning before the dash.
    private void UpdateShake()
    {
        if (mesh == null) return;

        bool shaking = stopPhase == StopPhase.Stopped && stopTimer >= stopTime - shakeTime;
        mesh.localPosition = meshHome + (shaking ? Random.insideUnitSphere * shakeMagnitude : Vector3.zero);
    }

    // Splitter: replaces this asteroid with splitCount smaller pieces side by side round the orbit, centred on its angle.
    private void Split()
    {
        hasSplit = true;

        Vector2 center = OrbitSettings.Instance.OrbitCenter;
        Vector2 fromCenter = (Vector2)transform.position - center;
        float angle = Mathf.Atan2(fromCenter.y, fromCenter.x);
        float radius = fromCenter.magnitude;

        for (int i = 0; i < splitCount; i++)
        {
            float pieceAngle = angle + (i - (splitCount - 1) / 2f) * splitSpread * Mathf.Deg2Rad;
            var position = new Vector3(center.x + Mathf.Cos(pieceAngle) * radius, center.y + Mathf.Sin(pieceAngle) * radius, transform.position.z);

            AsteroidBehavior piece = EffectPool.GetAsteroid(splitPiece, position);
            piece.BecomePiece(pieceScale);
            piece.gameObject.SetActive(true);
        }

        if (template != null) EffectPool.Play(template.explosionPrefab, transform.position);
        PatternCopy.Despawn(gameObject);
    }

    // Growing: pulses the mesh as it nears growDistance, then pops to its final size with an overshoot that settles.
    private void UpdateGrowing()
    {
        if (player == null || mesh == null) return;

        float gap = transform.position.z - player.transform.position.z;
        if (!hasGrown)
        {
            if (gap > growDistance)
            {
                float pulse = gap <= growDistance + pulseDistance ? Mathf.Sin(elapsed * pulseRate * Mathf.PI * 2f) * pulseSize : 0f;
                mesh.localScale = meshStartScale * (1f + pulse);
                return;
            }

            hasGrown = true;
            transform.localScale = startScale * growScale;
            hitRadius = fullHitRadius * growScale;
        }

        settleTimer += Time.deltaTime;
        float t = settleTime > 0f ? Mathf.Clamp01(settleTimer / settleTime) : 1f;
        mesh.localScale = meshStartScale * (1f + overshoot * (1f - t) * (1f - t));
    }

    // Magnet: pulls the player along the orbit toward this asteroid, harder the closer it is.
    private void UpdateMagnet()
    {
        if (player == null || magnetRadius <= 0f) return;

        Vector3 toMagnet = transform.position - player.transform.position;
        float distance = toMagnet.magnitude;
        if (distance >= magnetRadius) return;

        float closeness = 1f - distance / magnetRadius;
        Vector2 direction = ((Vector2)toMagnet).normalized;
        float alongOrbit = Vector2.Dot(direction, player.OrbitTangent);
        player.AddPull(magnetStrength * Mathf.Deg2Rad * closeness * closeness * alongOrbit);
    }

    // Frost: drops a patch each time the comet has travelled dropSpacing further.
    private void UpdateFrost()
    {
        float reach = patchRadius * patchRadius;
        bool inFrost = false;
        for (int i = patches.Count - 1; i >= 0; i--)
        {
            FrostPatch patch = patches[i];
            patch.age += Time.deltaTime;

            if (patch.age >= patchLifetime)
            {
                patches.RemoveAt(i);
                continue;
            }

            patches[i] = patch;
            if (player != null && (patch.position - player.transform.position).sqrMagnitude <= reach)
                inFrost = true;
        }

        if (inFrost) player.ApplyFrost(speedSlow, orbitSlow);
    }

    // Frost: near the player's depth, leaves a patch on the orbit ring under the comet, each time it has moved dropSpacing round the ring.
    private void PaintFrost()
    {
        if (dropSpacing <= 0f) return;

        OrbitSettings orbit = OrbitSettings.Instance;
        if (Mathf.Abs(transform.position.z - orbit.PlayerZ) > paintDistance) return;

        Vector2 fromCenter = (Vector2)transform.position - orbit.OrbitCenter;
        if (fromCenter.sqrMagnitude < 0.0001f) return;

        Vector2 onRing = orbit.OrbitCenter + fromCenter.normalized * orbit.OrbitRadius;
        Vector3 spot = new Vector3(onRing.x, onRing.y, orbit.PlayerZ);
        if (hasDropped && (spot - lastDrop).sqrMagnitude < dropSpacing * dropSpacing) return;

        patches.Add(new FrostPatch { position = spot, age = 0f });
        lastDrop = spot;
        hasDropped = true;
    }

    // Frost: draws each patch as a wire sphere, fading with age and red while the player is inside, so the trail can be seen before the particles exist.
    private void OnDrawGizmos()
    {
        float reach = patchRadius * patchRadius;
        for (int i = 0; i < patches.Count; i++)
        {
            bool inside = player != null && (patches[i].position - player.transform.position).sqrMagnitude <= reach;
            Color color = inside ? Color.red : new Color(0.5f, 0.9f, 1f);
            color.a = 1f - patches[i].age / patchLifetime;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(patches[i].position, patchRadius);
        }
    }

    // Despawns the asteroid, except a Frost comet that still has a trail: that one hides and waits for it to run out.
    private void Finish()
    {
        if (effectType != EffectType.Frost || patches.Count == 0)
        {
            PatternCopy.Despawn(gameObject);
            return;
        }

        retired = true;
        Active.Remove(this);
        if (trail != null) trail.Stop();
        if (rend != null) rend.enabled = false;
        if (col != null) col.enabled = false;
    }

    private void Update()
    {
        // Spins during the warning too, so asteroids appear already tumbling.
        Spin();
        UpdateOutline();
        UpdateDepthTint();

        if (!isMoving) return;

        if (retired)
        {
            UpdateFrost();
            if (patches.Count == 0) PatternCopy.Despawn(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        if (effectType == EffectType.Phase) UpdatePhase();

        // Approach speed rises with the player's speed, like the starfield, so
        // flying faster brings asteroids in faster. Distance is accumulated per
        // frame so a change in speed doesn't make the asteroid jump.
        float speedFactor = movementType == MovementType.StopAndGo ? StopAndGoFactor() : 1f;
        distance += ApproachSpeed * Time.deltaTime * speedFactor;
        if (movementType == MovementType.StopAndGo) UpdateShake();
        if (effectType == EffectType.Growing) UpdateGrowing();
        if (effectType == EffectType.Magnet) UpdateMagnet();

        // Every type travels toward the camera (-Z).
        Vector3 position = origin + Vector3.back * distance;

        // Wave snaps onto the player's circle and swings back and forth along it.
        if (movementType == MovementType.Wave)
        {
            // Read every frame so it follows the orbit being tuned live
            OrbitSettings orbit = OrbitSettings.Instance;
            float swing = Mathf.Sin(elapsed * frequency * Mathf.PI * 2f) * amplitude * Mathf.Deg2Rad * (MirrorSwing ? -1f : 1f);
            float angle = baseAngle + swing;
            position.x = orbit.OrbitCenter.x + Mathf.Cos(angle) * orbit.OrbitRadius;
            position.y = orbit.OrbitCenter.y + Mathf.Sin(angle) * orbit.OrbitRadius;
        }

        // Spiral drifts round the orbit at its own radius, in one direction.
        if (movementType == MovementType.Spiral)
        {
            OrbitSettings orbit = OrbitSettings.Instance;
            float angle = baseAngle + elapsed * spiralSpeed * Mathf.Deg2Rad * (MirrorSwing ? -1f : 1f);
            position.x = orbit.OrbitCenter.x + Mathf.Cos(angle) * startRadius;
            position.y = orbit.OrbitCenter.y + Mathf.Sin(angle) * startRadius;
        }

        // Homing turns toward the nearest player body until it's close, then flies straight at its own radius.
        if (movementType == MovementType.Homing)
        {
            if (!homingLocked && player != null)
            {
                if (transform.position.z - player.transform.position.z <= homingStopDistance)
                    homingLocked = true;
                else
                {
                    float turned = Mathf.MoveTowardsAngle(homingAngle * Mathf.Rad2Deg, ClosestBodyAngle() * Mathf.Rad2Deg,
                                                          homingSpeed * Time.deltaTime) * Mathf.Deg2Rad;
                    Vector2 center = OrbitSettings.Instance.OrbitCenter;
                    var ahead = new Vector3(center.x + Mathf.Cos(turned) * startRadius, center.y + Mathf.Sin(turned) * startRadius, transform.position.z);
                    if (!WouldCrowd(ahead)) homingAngle = turned;
                }
            }

            OrbitSettings orbit = OrbitSettings.Instance;
            position.x = orbit.OrbitCenter.x + Mathf.Cos(homingAngle) * startRadius;
            position.y = orbit.OrbitCenter.y + Mathf.Sin(homingAngle) * startRadius;
        }

        transform.position = position;
        if (effectType == EffectType.Frost)
        {
            UpdateFrost();
            PaintFrost();
        }
        if (closeCalls != null && player != null && !closeCallJudged && effectType != EffectType.Magnet) TrackCloseCall();

        // Asteroids fly toward -Z, so once one is far enough behind the camera it
        // can never come back into view.
        if (cam != null && template != null &&
            transform.position.z < cam.position.z - template.despawnBehindCamera)
        {
            Finish();
        }

        if (effectType == EffectType.Splitter && !hasSplit && splitPiece != null && player != null &&
            transform.position.z - player.transform.position.z <= splitDistance)
            Split();
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

        Finish();
    }
}
