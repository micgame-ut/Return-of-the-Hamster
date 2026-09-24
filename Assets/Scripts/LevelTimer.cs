using UnityEngine;

// Drives level progress by moving the earth toward the player over a set
// time. The faster the player flies, the sooner the earth arrives.
public class LevelTimer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform earth;
    [SerializeField] private Transform player;

    [Header("Timing")]
    [SerializeField] private float levelTime = 60f;        // seconds to arrive at player speed 0
    [SerializeField] private float speedTimeFactor = 0.1f; // how strongly player speed shortens levelTime

    [Header("Arrival")]
    [SerializeField] private float stopDistance = 20f;     // how far from the player the earth stops

    // 0 at level start, 1 when the earth has arrived
    public float Progress { get; private set; }
    public bool IsComplete => Progress >= 1f;

    private Vector3 origin;

    void Start()
    {
        origin = earth.position;
    }

    void Update()
    {
        if (IsComplete) return;

        // Player speed divides the level time: at Speed 10 with factor 0.1,
        // the level takes half as long. Progress is accumulated per frame
        // so a change in speed alters the pace without making the earth jump.
        float effectiveTime = levelTime / (1f + PlayerMovement.Speed * speedTimeFactor);
        Progress = Mathf.Min(Progress + Time.deltaTime / effectiveTime, 1f);

        // Stop point sits stopDistance short of the player, along the line
        // from the earth's starting position.
        Vector3 toPlayer = player.position - origin;
        Vector3 stopPoint = player.position - toPlayer.normalized * stopDistance;

        earth.position = Vector3.Lerp(origin, stopPoint, Progress);
    }
}
