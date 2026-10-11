using UnityEngine;

// Close-call range around the player and twin copies: a threatening asteroid that
// passes within closeCallDistance of the hitbox's edge without touching it is a close call.
public class CloseCallZone : MonoBehaviour
{
    [SerializeField] private float closeCallDistance = 1.5f; // gap past the hitbox edge that still counts as close

    public float Distance => closeCallDistance;
    public float Reach => shell != null ? shell.Reach : 0f;

    private PlayerShell shell;
    private PlayerTwins twins;

    private void Awake()
    {
        shell = GetComponent<PlayerShell>();
        twins = GetComponent<PlayerTwins>();
    }

    // Gap between an asteroid's edge and the nearest body's hitbox edge, and which body that is.
    public float NearestGap(Vector3 asteroidPosition, float asteroidRadius, out Vector3 bodyPosition)
    {
        bodyPosition = transform.position;
        float nearest = Vector2.Distance(asteroidPosition, bodyPosition);

        if (twins != null)
        {
            for (int i = 0; i < twins.Copies.Count; i++)
            {
                Vector3 copyPosition = twins.Copies[i].transform.position;
                float distance = Vector2.Distance(asteroidPosition, copyPosition);
                if (distance < nearest)
                {
                    nearest = distance;
                    bodyPosition = copyPosition;
                }
            }
        }

        return nearest - asteroidRadius - Reach;
    }

    private void OnDrawGizmosSelected()
    {
        PlayerShell gizmoShell = shell != null ? shell : GetComponent<PlayerShell>();
        float reach = gizmoShell != null ? gizmoShell.Reach : 0f;

        DrawZone(transform.position, reach);
        if (twins != null)
            for (int i = 0; i < twins.Copies.Count; i++)
                DrawZone(twins.Copies[i].transform.position, reach);
    }

    private void DrawZone(Vector3 center, float reach)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, reach + closeCallDistance);
    }
}
