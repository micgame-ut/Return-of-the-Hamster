using UnityEngine;

// A copy made by the twin power-up. No input or health of its own: it sits at an
// offset around the orbit from the player and copies its movement, roll and shell.
public class TwinCopy : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;
    [SerializeField] private GameObject shellMesh;
    [SerializeField] private Collider shellCollider;
    [SerializeField] private Collider bodyCollider;

    private PlayerMovement player;
    private PlayerShell playerShell;
    private Renderer shellRenderer;

    private float offset;       // radians around the orbit from the player
    private float fromOffset;   // where the current re-space starts
    private float toOffset;     // where the current re-space ends
    private float moveTime;     // seconds into the current re-space
    private float moveDuration; // length of the current re-space; 0 = none running

    private void Awake()
    {
        GameManager gm = GameManager.Instance;
        player = gm != null ? gm.Player : null;
        playerShell = gm != null ? gm.PlayerShell : null;
        shellRenderer = shellMesh != null ? shellMesh.GetComponentInChildren<Renderer>(true) : null;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        if (moveDuration > 0f)
        {
            moveTime += Time.deltaTime;
            offset = Mathf.Lerp(fromOffset, toOffset, Mathf.SmoothStep(0f, 1f, moveTime / moveDuration));
            if (moveTime >= moveDuration) moveDuration = 0f;
        }

        float angle = player.OrbitAngle + offset;
        Vector2 center = player.OrbitCenter;
        float radius = player.CircleRadius;
        transform.position = new Vector3(center.x + Mathf.Cos(angle) * radius,
                                         center.y + Mathf.Sin(angle) * radius,
                                         player.transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, offset * Mathf.Rad2Deg) * player.transform.rotation;

        if (meshTransform != null) meshTransform.localRotation = player.MeshTransform.localRotation;
    }

    public float Offset => offset;

    // Eases to a new offset over duration seconds; 0 = straight there.
    public void MoveTo(float newOffset, float duration)
    {
        fromOffset = offset;
        toOffset = newOffset;
        moveTime = 0f;
        moveDuration = duration;
        if (duration <= 0f) offset = newOffset;
    }

    // Called when the player jumps, so the copy stays put in the world.
    public void ShiftOffset(float radians)
    {
        offset += radians;
        fromOffset += radians;
        toOffset += radians;
    }

    private void OnEnable()
    {
        if (playerShell != null) playerShell.OnShellChanged += ApplyShell;
        ApplyShell();
    }

    private void OnDisable()
    {
        if (playerShell != null) playerShell.OnShellChanged -= ApplyShell;
    }

    private void ApplyShell()
    {
        if (playerShell == null) return;
        if (shellCollider != null) shellCollider.enabled = playerShell.Shielded;
        if (bodyCollider != null) bodyCollider.enabled = !playerShell.Shielded;
        if (shellMesh != null) shellMesh.SetActive(playerShell.ShellShowing);
        if (shellRenderer != null) shellRenderer.sharedMaterial = playerShell.ShellMaterial;
    }
}
