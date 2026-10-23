using System;
using UnityEngine;

// Timed invincibility from a power-up. While active, asteroids are busted on
// contact, and PlayerShell shows the rainbow (flickering near the end).
public class PlayerInvincibility : MonoBehaviour
{
    [Tooltip("Seconds before the end when the rainbow starts flickering.")]
    [SerializeField] private float warningTime = 1f;
    [Tooltip("Seconds between each flicker on/off.")]
    [SerializeField] private float warningBlinkInterval = 0.1f;
    [Tooltip("While invincible, PlayerMovement holds the player at max speed.")]
    [SerializeField] private bool lockMaxSpeed = true;

    public bool IsInvincible => timer > 0f;
    // True while invincible with lockMaxSpeed on; PlayerMovement holds the player at max speed then.
    public bool LocksMaxSpeed => lockMaxSpeed && IsInvincible;

    public event Action<bool> OnInvincibilityChanged;
    public bool RainbowShowing => overlayOn;
    public event Action<bool> OnRainbowChanged;

    private float timer;
    private bool overlayOn;

    private void SetOverlay(bool on)
    {
        if (on == overlayOn) return;
        overlayOn = on;
        OnRainbowChanged?.Invoke(on);
    }

    public void Activate(float duration)
    {
        if (duration <= 0f) return;

        bool wasInvincible = IsInvincible;
        timer = duration;
        SetOverlay(true);
        if (!wasInvincible) OnInvincibilityChanged?.Invoke(true);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleLevelEnded;
            GameManager.Instance.OnWin += HandleLevelEnded;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleLevelEnded;
            GameManager.Instance.OnWin -= HandleLevelEnded;
        }
    }

    private void HandleLevelEnded()
    {
        if (!IsInvincible) return;
        timer = 0f;
        SetOverlay(false);
        OnInvincibilityChanged?.Invoke(false);
    }

    private void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SetOverlay(false);
            OnInvincibilityChanged?.Invoke(false);
            return;
        }

        bool visible = timer > warningTime ||
                       Mathf.FloorToInt(timer / warningBlinkInterval) % 2 == 0;
        SetOverlay(visible);
    }
}
