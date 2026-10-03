using UnityEngine;

// A shell around the ship: shows the rainbow while invincible, the shield at
// full health, or nothing. While shielded or invincible, its collider makes
// the player's hitbox bigger.
public class PlayerShell : MonoBehaviour
{
    [SerializeField] private GameObject shellMesh;
    [SerializeField] private Collider shellCollider;
    [SerializeField] private Material shieldMaterial;
    [SerializeField] private Material rainbowMaterial;

    private PlayerHealth health;
    private PlayerInvincibility invincibility;
    private Renderer shellRenderer;

    // XY footprint of the shell's hitbox for OrbitRing; 0 while it's off.
    public float HitRadius => shellCollider != null && shellCollider.enabled
        ? Mathf.Max(shellCollider.bounds.extents.x, shellCollider.bounds.extents.y)
        : 0f;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        invincibility = GetComponent<PlayerInvincibility>();
        shellRenderer = shellMesh != null ? shellMesh.GetComponentInChildren<Renderer>(true) : null;
    }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (health != null) health.OnLivesChanged += HandleLivesChanged;
        if (invincibility != null)
        {
            invincibility.OnInvincibilityChanged += HandleStateChanged;
            invincibility.OnRainbowChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (health != null) health.OnLivesChanged -= HandleLivesChanged;
        if (invincibility != null)
        {
            invincibility.OnInvincibilityChanged -= HandleStateChanged;
            invincibility.OnRainbowChanged -= HandleStateChanged;
        }
    }

    private void HandleLivesChanged(int lives) => Refresh();
    private void HandleStateChanged(bool on) => Refresh();

    private void Refresh()
    {
        if (health == null) return;

        bool fullHealth = health.CurrentLives >= health.MaxLives;
        bool invincible = invincibility != null && invincibility.IsInvincible;
        bool rainbow = invincibility != null && invincibility.RainbowShowing;

        if (shellCollider != null) shellCollider.enabled = fullHealth || invincible;

        if (shellMesh == null) return;
        shellMesh.SetActive(rainbow || fullHealth);
        if (shellRenderer != null) shellRenderer.sharedMaterial = rainbow ? rainbowMaterial : shieldMaterial;
    }
}
