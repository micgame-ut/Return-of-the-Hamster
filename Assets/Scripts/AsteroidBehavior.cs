using System.Collections;
using UnityEngine;

// Moves an asteroid toward the camera using one of several selectable
// movement patterns, chosen from a dropdown in the inspector. Before it
// starts moving, it blinks a warning glow so the player can see it coming.
public class AsteroidBehavior : MonoBehaviour
{
    public enum MovementType { Linear, Wave, Spiral }

    [Header("Movement")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    [SerializeField] private float speed = 20f;

    [Header("Wave / Spiral")]
    [SerializeField] private float amplitude = 5f;   // wave height or spiral radius
    [SerializeField] private float frequency = 1f;   // cycles per second

    [Header("Destruction")]
    [SerializeField] private bool destructible = false;
    [SerializeField] private float destroySpeed = 5f; // player Speed needed to break it (Speed ranges 0 to PlayerMovement.MaxSpeed)

    [Header("Template")]
    [SerializeField] private AsteroidTemplate template;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    private Renderer rend;
    private Collider col;
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock outlineBlock;
    private Transform mesh;
    private Transform cam;
    private int outlineIndex = -1; // material slot of the outline, -1 if none
    private int outlineState = -1; // 0 too slow, 1 can break, -1 not yet set

    private Vector3 origin;
    private float elapsed;
    private float distance; // how far it has travelled toward the camera
    private bool isMoving;
    private Vector3 spinAxis;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        block = new MaterialPropertyBlock();

        // The renderer lives on the child mesh object, so that's what spins.
        mesh = rend != null ? rend.transform : null;
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;

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
        origin = transform.position;
        elapsed = 0f;
        distance = 0f;
        isMoving = false;
        outlineState = -1; // force the outline color to refresh

        // Each asteroid spins around its own random axis.
        spinAxis = Random.onUnitSphere;

        StartCoroutine(WarnThenMove());
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
        int state = PlayerMovement.Speed >= destroySpeed ? 1 : 0;
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
        float multiplier = template != null ? template.speedMultiplier : 0f;
        distance += (speed + PlayerMovement.Speed * multiplier) * Time.deltaTime;

        // Every type travels toward the camera (-Z); the types differ only
        // in the sideways offset added on top of that forward motion.
        Vector3 forward = Vector3.back * distance;
        float angle = elapsed * frequency * Mathf.PI * 2f;

        Vector3 offset = movementType switch
        {
            MovementType.Wave => new Vector3(Mathf.Sin(angle) * amplitude, 0f, 0f),
            MovementType.Spiral => new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * amplitude,
            // Linear and any other unhandled types have no offset.
            _ => Vector3.zero,
        };

        transform.position = origin + forward + offset;

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

        if (destructible && PlayerMovement.Speed >= destroySpeed)
        {
            Explode();
        }
        else if (template != null)
        {
            // Push the player away from the asteroid, toward the far side of the
            // screen. A dead-center hit picks a side at random.
            float side = player.transform.position.x - transform.position.x;
            float dir = side != 0f ? Mathf.Sign(side) : (Random.value < 0.5f ? -1f : 1f);
            if (player.TakeHit(dir * template.knockbackForce, template.speedLoss))
                CameraShake.Shake(template.hitShakeDuration, template.hitShakeMagnitude);
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

        Destroy(gameObject);
    }
}
