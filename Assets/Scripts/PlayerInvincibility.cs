using System;
using UnityEngine;

// Timed invincibility from a power-up. While active, asteroids are busted on
// contact, and PlayerShell shows the rainbow (flickering near the end).
public class PlayerInvincibility : MonoBehaviour
{
    [SerializeField] private float warningTime = 1f;
    [SerializeField] private float warningBlinkInterval = 0.1f;
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

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGameOver || gm.HasWon))
        {
            if (IsInvincible)
            {
                timer = 0f;
                SetOverlay(false);
                OnInvincibilityChanged?.Invoke(false);
            }
            return;
        }

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
