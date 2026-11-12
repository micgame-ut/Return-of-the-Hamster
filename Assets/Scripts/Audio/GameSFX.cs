using UnityEngine;
using UnityEngine.SceneManagement;

// Plays a sound for each game event. Every slot takes clips by drag and drop, and an empty slot plays nothing.
// Sits on the AudioManager (it survives scene loads) and plays through SFXManager.
[DisallowMultipleComponent]
public class GameSFX : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        [Tooltip("Drag clips here. One is picked at random each time, never the same one twice in a row. Empty = no sound.")]
        public AudioClip[] clips;
        [Tooltip("How loud this sound is, from 0 (silent) to 1 (full).")]
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("A random pitch between X and Y each time. 1 and 1 = no variation, below 1 is lower, above 1 is higher.")]
        public Vector2 pitchRange = new Vector2(1f, 1f);

        [System.NonSerialized] public int lastIndex = -1;
    }

    [System.Serializable]
    public class DialogueSlot : Slot
    {
        [Tooltip("The chance the line plays when its event happens: 0 = never, 0.3 = about 3 times in 10, 1 = every time.")]
        [Range(0f, 1f)] public float chance = 0.3f;
    }

    [Header("Player")]
    [Tooltip("The player loses a life.")]
    [SerializeField] private Slot hit;
    [Tooltip("The player gains a life.")]
    [SerializeField] private Slot lifeGained;
    [Tooltip("A boost around the orbit.")]
    [SerializeField] private Slot sideBoost;
    [Tooltip("A forward boost.")]
    [SerializeField] private Slot forwardBoost;
    [Tooltip("The boost cooldown has finished, so a boost is ready again.")]
    [SerializeField] private Slot boostReady;

    [Header("Power-ups")]
    [Tooltip("A Health power-up is collected.")]
    [SerializeField] private Slot healthPickup;
    [Tooltip("An Invincibility power-up is collected.")]
    [SerializeField] private Slot invinciblePickup;
    [Tooltip("A Twin power-up is collected.")]
    [SerializeField] private Slot twinPickup;
    [Tooltip("A Score Multiplier power-up is collected.")]
    [SerializeField] private Slot scorePickup;
    [Tooltip("Invincibility starts.")]
    [SerializeField] private Slot invincibleStart;
    [Tooltip("Invincibility runs out.")]
    [SerializeField] private Slot invincibleEnd;
    [Tooltip("The twin starts (the first copy appears).")]
    [SerializeField] private Slot twinStart;
    [Tooltip("The twin ends (the last copy is gone).")]
    [SerializeField] private Slot twinEnd;
    [Tooltip("A twin body is destroyed by a hit. Plays together with Twin End when the last copy goes.")]
    [SerializeField] private Slot twinBodyLost;

    [Header("Asteroids")]
    [Tooltip("The player smashes an asteroid.")]
    [SerializeField] private Slot asteroidSmashed;
    [Tooltip("The player smashes a BonusCarrier asteroid. Plays instead of Asteroid Smashed.")]
    [SerializeField] private Slot bonusSmashed;
    [Tooltip("A Splitter asteroid bursts into pieces.")]
    [SerializeField] private Slot splitterBurst;
    [Tooltip("A close call is scored.")]
    [SerializeField] private Slot closeCall;

    [Header("Level")]
    [Tooltip("The level starts, after the menu transition finishes.")]
    [SerializeField] private Slot levelStart;
    [Tooltip("The level is won.")]
    [SerializeField] private Slot win;
    [Tooltip("The game is over, at the moment the last life is lost.")]
    [SerializeField] private Slot gameOver;
    [Tooltip("The player's death explosion, at the end of the death sequence.")]
    [SerializeField] private Slot playerExplosion;
    [Tooltip("The player reaches overdrive speed.")]
    [SerializeField] private Slot overdriveStart;
    [Tooltip("The player drops out of overdrive.")]
    [SerializeField] private Slot overdriveEnd;

    [Header("Dialogue")]
    [Tooltip("Seconds that must pass after a line starts before another can play, so he never talks over himself. Set it longer than your longest line. A line that would start too soon is skipped.")]
    [SerializeField, Min(0f)] private float minLineGap = 2f;
    [Tooltip("When the player loses a life. The last life uses Game Over Line instead.")]
    [SerializeField] private DialogueSlot hitLine;
    [Tooltip("When the level starts, after the menu transition.")]
    [SerializeField] private DialogueSlot startLine;
    [Tooltip("When a close call is scored. It happens often, so a low chance suits it.")]
    [SerializeField] private DialogueSlot closeCallLine;
    [Tooltip("When the level is won.")]
    [SerializeField] private DialogueSlot winLine;
    [Tooltip("When the hamster reaches the screen during the death animation.")]
    [SerializeField] private DialogueSlot gameOverLine;
    [Tooltip("When a Health power-up is collected.")]
    [SerializeField] private DialogueSlot healthLine;
    [Tooltip("When an Invincibility power-up is collected.")]
    [SerializeField] private DialogueSlot invincibleLine;
    [Tooltip("When a Twin power-up is collected.")]
    [SerializeField] private DialogueSlot twinLine;
    [Tooltip("When a Score Multiplier power-up is collected.")]
    [SerializeField] private DialogueSlot scoreLine;

    private static GameSFX instance;
    private MusicSpeedDriver musicSpeed;
    private int lastLives; // the life count last time it changed, to tell gaining a life from losing one
    private float nextLineTime; // the time from which the next line may start; unscaled, so it still counts after game over

    // This level's objects. Bind finds them again each time a level loads.
    private GameManager gameManager;
    private PlayerHealth health;
    private PlayerMovement movement;
    private PlayerInvincibility invincibility;
    private PlayerTwins twins;

    private void Awake()
    {
        // A reloaded Level 1 brings a second AudioManager; only the first GameSFX stays.
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        musicSpeed = GetComponent<MusicSpeedDriver>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnEnable()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        PowerUp.OnCollected += HandlePowerUpCollected;
        AsteroidBehavior.OnSmashed += HandleSmashed;
        AsteroidBehavior.OnSplit += HandleSplit;
        AsteroidBehavior.OnCloseCall += HandleCloseCall;
        PlayerDeath.OnExploded += HandleExploded;
        PlayerDeath.OnArrived += HandleDeathArrived;
        if (musicSpeed != null) musicSpeed.OverdriveChanged += HandleOverdriveChanged;
        Bind();
    }

    private void OnDisable()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PowerUp.OnCollected -= HandlePowerUpCollected;
        AsteroidBehavior.OnSmashed -= HandleSmashed;
        AsteroidBehavior.OnSplit -= HandleSplit;
        AsteroidBehavior.OnCloseCall -= HandleCloseCall;
        PlayerDeath.OnExploded -= HandleExploded;
        PlayerDeath.OnArrived -= HandleDeathArrived;
        if (musicSpeed != null) musicSpeed.OverdriveChanged -= HandleOverdriveChanged;
        Unbind();
    }

    // Every level load brings new objects, so the per-level subscriptions are made again.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Bind();

    // Plays one slot: picks a clip at random (never the one just played), then a random pitch. An empty slot does nothing.
    public static void Play(Slot slot) =>
        Play(slot, slot != null ? Random.Range(slot.pitchRange.x, slot.pitchRange.y) : 1f);

    public static void Play(Slot slot, float pitch)
    {
        SFXManager sfx = SFXManager.Instance;
        if (slot == null || sfx == null || slot.clips == null || slot.clips.Length == 0) return;

        int index = Random.Range(0, slot.clips.Length);
        // If the pick repeats the last clip, step to a different one: jump ahead 1 plus a random 0 to (count - 2), wrapping around.
        if (slot.clips.Length > 1 && index == slot.lastIndex)
            index = (index + 1 + Random.Range(0, slot.clips.Length - 1)) % slot.clips.Length;
        slot.lastIndex = index;

        sfx.PlayClip(slot.clips[index], slot.volume, pitch);
    }

    // Rolls the slot's chance and, if it hits and the last line was far enough back, plays one of its lines.
    private void Say(DialogueSlot slot)
    {
        if (slot == null || slot.clips == null || slot.clips.Length == 0) return;
        if (slot.chance <= 0f || Time.unscaledTime < nextLineTime || Random.value > slot.chance) return;

        Play(slot);
        nextLineTime = Time.unscaledTime + minLineGap;
    }

    private void HandleLifeLost()
    {
        Play(hit);
        // The last life's hit is voiced by Game Over Line instead, which plays when the hamster reaches the screen.
        if (health == null || health.CurrentLives > 0) Say(hitLine);
    }

    // OnLivesChanged fires for gains and losses and only carries the new count, so a higher count than last time is a gained life.
    private void HandleLivesChanged(int lives)
    {
        if (lives > lastLives) Play(lifeGained);
        lastLives = lives;
    }

    // The direction isn't needed here; the event just requires the parameter.
    private void HandleSideBoost(float direction) => Play(sideBoost);

    private void HandleForwardBoost() => Play(forwardBoost);

    private void HandleBoostReady() => Play(boostReady);

    // OnCollected reports the type the pickup actually rolled, so a Random power-up plays the sound of what it became.
    private void HandlePowerUpCollected(PowerUp.PowerUpType type, Vector3 position)
    {
        switch (type)
        {
            case PowerUp.PowerUpType.Health: Play(healthPickup); Say(healthLine); break;
            case PowerUp.PowerUpType.Invincible: Play(invinciblePickup); Say(invincibleLine); break;
            case PowerUp.PowerUpType.Twin: Play(twinPickup); Say(twinLine); break;
            case PowerUp.PowerUpType.ScoreMultiplier: Play(scorePickup); Say(scoreLine); break;
        }
    }

    // One event covers both the start and the end, so the bool picks the slot.
    private void HandleInvincibilityChanged(bool on) => Play(on ? invincibleStart : invincibleEnd);

    private void HandleTwinChanged(bool on) => Play(on ? twinStart : twinEnd);

    private void HandleBodyLost() => Play(twinBodyLost);

    // A BonusCarrier plays only its own sound; every other kind uses the normal smash sound.
    private void HandleSmashed(AsteroidBehavior.AsteroidKind kind) =>
        Play(kind == AsteroidBehavior.AsteroidKind.BonusCarrier ? bonusSmashed : asteroidSmashed);

    private void HandleSplit() => Play(splitterBurst);

    private void HandleCloseCall()
    {
        Play(closeCall);
        Say(closeCallLine);
    }

    private void HandleGameStarted()
    {
        Play(levelStart);
        Say(startLine);
    }

    private void HandleWin()
    {
        Play(win);
        Say(winLine);
    }

    private void HandleGameOver() => Play(gameOver);

    private void HandleDeathArrived() => Say(gameOverLine);

    private void HandleExploded() => Play(playerExplosion);

    // OverdriveChanged sends true when overdrive starts and false when it ends.
    private void HandleOverdriveChanged(bool on) => Play(on ? overdriveStart : overdriveEnd);

    // Finds this level's objects and subscribes to their events. Safe to call twice, because it unbinds first.
    private void Bind()
    {
        Unbind();

        gameManager = GameManager.Instance;
        if (gameManager == null) return;

        health = gameManager.PlayerHealth;
        movement = gameManager.Player;
        invincibility = gameManager.PlayerInvincibility;
        twins = gameManager.PlayerTwins;

        gameManager.OnGameStarted += HandleGameStarted;
        gameManager.OnWin += HandleWin;
        gameManager.OnGameOver += HandleGameOver;
        if (health != null)
        {
            lastLives = health.CurrentLives;
            health.OnLifeLost += HandleLifeLost;
            health.OnLivesChanged += HandleLivesChanged;
        }
        if (movement != null)
        {
            movement.OnSideBoost += HandleSideBoost;
            movement.OnSpeedBoost += HandleForwardBoost;
            movement.OnBoostReady += HandleBoostReady;
        }
        if (invincibility != null) invincibility.OnInvincibilityChanged += HandleInvincibilityChanged;
        if (twins != null)
        {
            twins.OnTwinChanged += HandleTwinChanged;
            twins.OnBodyLost += HandleBodyLost;
        }
    }

    // Drops every subscription to the previous level's objects. Destroyed objects read as null, so they're skipped.
    private void Unbind()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStarted -= HandleGameStarted;
            gameManager.OnWin -= HandleWin;
            gameManager.OnGameOver -= HandleGameOver;
        }
        if (health != null)
        {
            health.OnLifeLost -= HandleLifeLost;
            health.OnLivesChanged -= HandleLivesChanged;
        }
        if (movement != null)
        {
            movement.OnSideBoost -= HandleSideBoost;
            movement.OnSpeedBoost -= HandleForwardBoost;
            movement.OnBoostReady -= HandleBoostReady;
        }
        if (invincibility != null) invincibility.OnInvincibilityChanged -= HandleInvincibilityChanged;
        if (twins != null)
        {
            twins.OnTwinChanged -= HandleTwinChanged;
            twins.OnBodyLost -= HandleBodyLost;
        }
    }
}
