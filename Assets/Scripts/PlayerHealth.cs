using UnityEngine;

// Tracks the player's lives. Lose one with LoseLife(); when none are left,
// it tells GameManager the game is over.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxLives = 3;

    public int MaxLives => maxLives;
    public int CurrentLives { get; private set; }

    private void Awake()
    {
        CurrentLives = maxLives;
    }

    public void LoseLife()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver || gm.HasWon) return;

        CurrentLives--;
        Debug.Log($"Lives left: {CurrentLives}");

        if (CurrentLives <= 0)
            gm.GameOver();
    }
}
