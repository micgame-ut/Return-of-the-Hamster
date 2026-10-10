using UnityEngine;

// Drives level progress by moving the earth toward the earth camera over a
// set time. The faster the player flies, the sooner the earth arrives.
public class LevelTimer : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float levelTime = 60f;        // seconds to arrive at player speed 0
    [SerializeField] private float speedTimeFactor = 0.1f; // how strongly player speed shortens levelTime

    [Header("Arrival")]
    [SerializeField] private float stopDistance = 20f;     // how far from the earth camera the earth stops

    // 0 at level start, 1 when the earth has arrived
    public float Progress { get; private set; }
    public bool IsComplete => Progress >= 1f;
    public float LevelTime => levelTime;

    private Transform earth;
    private Vector3 origin;
    private Vector3 stopPoint;

    void Start()
    {
        earth = GameManager.Instance.Earth.transform;
        origin = earth.position;

        // Stop point sits stopDistance short of the earth camera, along the line
        // from the earth's starting position, so the earth never passes the
        // camera that renders it. The camera doesn't move, so this is fixed.
        Vector3 cameraPosition = GameManager.Instance.EarthCamera.transform.position;
        Vector3 toCamera = cameraPosition - origin;
        stopPoint = cameraPosition - toCamera.normalized * stopDistance;
    }

    void Update()
    {
        if (IsComplete || GameManager.Instance.IsGameOver) return;

        // Player speed divides the level time: at Speed 10 with factor 0.1,
        // the level takes half as long. Progress is accumulated per frame
        // so a change in speed alters the pace without making the earth jump.
        float effectiveTime = levelTime / (1f + PlayerMovement.Speed * speedTimeFactor);
        Progress = Mathf.Min(Progress + Time.deltaTime / effectiveTime, 1f);

        earth.position = Vector3.Lerp(origin, stopPoint, Progress);
    }
}
