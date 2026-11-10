using System;
using UnityEngine;

// Tracks the player's lives. Lose one with LoseLife(); when none are left,
// it tells GameManager the game is over.
public class PlayerHealth : MonoBehaviour
{
    [Tooltip("How many lives the player starts with, which is the number of counted hits that ends the game.")]
    [SerializeField] private int maxLives = 3;

    public int MaxLives => maxLives;
    public int CurrentLives { get; private set; }

    public event Action<int> OnLivesChanged;
    public event Action OnLifeLost;

    private void Awake()
    {
        CurrentLives = maxLives;
    }

    public void LoseLife()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver || gm.HasWon || gm.InTutorial) return;

        CurrentLives--;
        OnLivesChanged?.Invoke(CurrentLives);
        OnLifeLost?.Invoke();

        if (CurrentLives <= 0)
            gm.GameOver();
    }

    public void GainLife()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver || gm.HasWon) return;

        CurrentLives = Mathf.Min(CurrentLives + 1, maxLives);
        OnLivesChanged?.Invoke(CurrentLives);
    }
}
