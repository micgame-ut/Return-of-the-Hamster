using System;
using UnityEngine;

// Timed Slim from a power-up: the player and its twin copies shrink, hold, then grow slowly back. The colliders scale
// with the objects, so the hit area, the close-call zone and the shield all follow.
public class PlayerSlim : MonoBehaviour
{
    public bool IsActive => timer > 0f;
    public float Scale => current;   // 1 = full size

    public event Action<bool> OnSlimChanged;   // true when Slim starts, false when it ends

    private float timer;         // seconds left
    private float current = 1f;  // the size right now
    private float slimSize = 1f; // the size it holds while Slim lasts
    private float growSeconds;   // the grow-back at the end starts when this many seconds are left
    private float shrinkRate;    // size lost per second on the way down
    private float growRate;      // size gained per second on the way back up
    private PlayerTwins twins;

    private void Awake()
    {
        twins = GetComponent<PlayerTwins>();
    }

    // Starts Slim, or restarts it at the full duration. The rates are worked out so the shrink and the grow-back each take exactly their time.
    public void Activate(float duration, float scale, float shrinkTime, float growTime)
    {
        if (duration <= 0f) return;

        bool wasActive = IsActive;
        timer = duration;
        slimSize = scale;
        growSeconds = growTime;
        shrinkRate = shrinkTime > 0f ? (1f - scale) / shrinkTime : float.MaxValue;
        growRate = growTime > 0f ? (1f - scale) / growTime : float.MaxValue;
        if (!wasActive) OnSlimChanged?.Invoke(true);
    }

    private void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0f;
                OnSlimChanged?.Invoke(false);
            }
        }

        // Heading for the small size until the last growSeconds, then for full size.
        float target = IsActive && timer > growSeconds ? slimSize : 1f;
        float rate = target < current ? shrinkRate : growRate;
        float next = Mathf.MoveTowards(current, target, rate * Time.deltaTime);

        bool changed = next != current;
        current = next;
        if (changed || current != 1f) ApplyScale();
    }

    private void ApplyScale()
    {
        transform.localScale = Vector3.one * current;
        if (twins == null) return;
        for (int i = 0; i < twins.Copies.Count; i++)
            twins.Copies[i].transform.localScale = Vector3.one * current;
    }

    private void OnEnable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver += HandleLevelEnded;
        GameManager.Instance.OnWin += HandleLevelEnded;
    }

    private void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver -= HandleLevelEnded;
        GameManager.Instance.OnWin -= HandleLevelEnded;
    }

    private void HandleLevelEnded()
    {
        bool wasActive = IsActive;
        timer = 0f;
        current = 1f;
        ApplyScale();
        if (wasActive) OnSlimChanged?.Invoke(false);
    }
}
