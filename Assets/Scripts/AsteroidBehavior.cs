using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Moves an asteroid toward the camera using one of several selectable
// movement patterns, chosen from a dropdown in the inspector. Before it
// starts moving, it blinks a warning glow so the player can see it coming.
public class AsteroidBehavior : MonoBehaviour
{
    public enum MovementType { Linear, Wave }

    [Header("Movement")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    [SerializeField] private float speed = 20f;

    [Header("Wave")]
    [SerializeField] private float amplitude = 30f;  // degrees swung each way around the orbit
    [SerializeField] private float frequency = 1f;   // cycles per second

    [Header("Destruction")]
    [SerializeField] private bool destructible = false;
    [SerializeField] private float destroySpeed = 5f; // player Speed needed to break it (Speed ranges 0 to PlayerMovement.MaxSpeed)

    [Header("Template")]
    [SerializeField] private SpawnableTemplate template;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    private Renderer rend;
    private Collider col;
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock outlineBlock;
    private Transform mesh;
    private Transform cam;
    private PlayerInvincibility invincibility;
    private int outlineIndex = -1; // material slot of the outline, -1 if none
    private int outlineState = -1; // 0 too slow, 1 can break, -1 not yet set

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
    public bool CanBreak => (invincibility != null && invincibility.IsInvincible) ||
                            (destructible && PlayerMovement.Speed >= destroySpeed);

    // XY footprint of the collider; its bounds are only valid while it's enabled.
    public float HitRadius => col != null && col.enabled ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) : 0f;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        block = new MaterialPropertyBlock();

        // The renderer lives on the child mesh object, so that's what spins.
        mesh = rend != null ? rend.transform : null;
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
        invincibility = GameManager.Instance != null ? GameManager.Instance.PlayerInvincibility : null;

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
                yield return new WaitForSeconds(template.blinkInterval);
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

    private void Spin()
    {
        if (mesh == null || template == null) return;
        mesh.Rotate(spinAxis, template.spinSpeed * Time.deltaTime, Space.World);
    }

    private void UpdateOutline()
    {
        if (outlineIndex < 0) return;

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

        // Asteroids fly toward -Z, so once one is far enough behind the camera it
        // can never come back into view.
        if (cam != null && template != null &&
            transform.position.z < cam.position.z - template.despawnBehindCamera)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react to the player. GetComponentInParent covers the collider
        // being on a child of the object that has PlayerMovement.
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        if (CanBreak)
        {
            Explode();
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

                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null) health.LoseLife();
            }
        }
    }

    private void Explode()
    {
        if (template != null)
        {
            if (template.explosionPrefab != null)
                Instantiate(template.explosionPrefab, transform.position, Quaternion.identity);

            CameraShake.Shake(template.shakeDuration, template.shakeMagnitude);
        }

        RunStats stats = GameManager.Instance != null ? GameManager.Instance.RunStats : null;
        if (stats != null) stats.AddAsteroidDestroyed();

        Destroy(gameObject);
    }
}
