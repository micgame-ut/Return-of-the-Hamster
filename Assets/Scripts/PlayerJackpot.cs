using System;
using UnityEngine;

// Timed Jackpot from a power-up. While it's active, every asteroid counts as a smashable Bonus rock (AsteroidBehavior
// checks IsActive), and ColorShowing says whether they should look gold right now.
public class PlayerJackpot : MonoBehaviour
{
    [Tooltip("Seconds between each flicker of the gold at the end of Jackpot.")]
    [SerializeField] private float blinkInterval = 0.1f;

    public bool IsActive => timer > 0f;
    // True while the asteroids should look gold: all through Jackpot, then flickering in the last warning seconds.
    public bool ColorShowing => IsActive && showing;

    public event Action<bool> OnJackpotChanged;   // true when Jackpot starts, false when it ends

    private float timer;
    private float warning;   // seconds before the end that the flicker starts
    private bool showing;

    // Starts Jackpot, or restarts it at the full duration. warningTime is how long before the end the gold starts to flicker.
    public void Activate(float duration, float warningTime)
    {
        if (duration <= 0f) return;

        bool wasActive = IsActive;
        timer = duration;
        warning = warningTime;
        showing = true;
        if (!wasActive) OnJackpotChanged?.Invoke(true);
    }

    private void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            showing = false;
            OnJackpotChanged?.Invoke(false);
            return;
        }

        showing = timer > warning || Mathf.FloorToInt(timer / blinkInterval) % 2 == 0;
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
        if (!IsActive) return;
        timer = 0f;
        showing = false;
        OnJackpotChanged?.Invoke(false);
    }
}
