using System;
using UnityEngine;

// A shell around the ship: shows the rainbow while invincible, the shield at
// full health, or nothing. While shielded or invincible, its collider makes
// the player's hitbox bigger.
public class PlayerShell : MonoBehaviour
{
    [SerializeField] private GameObject shellMesh;
    [SerializeField] private Collider shellCollider;
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private Material shieldMaterial;
    [SerializeField] private Material rainbowMaterial;

    private PlayerHealth health;
    private PlayerInvincibility invincibility;
    private PlayerTwins twins;
    private Renderer shellRenderer;

    // XY footprint of the shell's hitbox for OrbitRing; 0 while it's off.
    public float HitRadius => Extent(shellCollider);

    // XY footprint of whichever hitbox is on (body or shell), for close calls.
    public float Reach => Mathf.Max(Extent(bodyCollider), Extent(shellCollider));

    private static float Extent(Collider c) =>
        c != null && c.enabled ? Mathf.Max(c.bounds.extents.x, c.bounds.extents.y) : 0f;

    // Shell state for TwinCopy to mirror; OnShellChanged fires after every Refresh.
    public event Action OnShellChanged;
    public bool Shielded { get; private set; }
    public bool ShellShowing => shellMesh != null && shellMesh.activeSelf;
    public Material ShellMaterial => shellRenderer != null ? shellRenderer.sharedMaterial : null;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        invincibility = GetComponent<PlayerInvincibility>();
        twins = GetComponent<PlayerTwins>();
        shellRenderer = shellMesh != null ? shellMesh.GetComponentInChildren<Renderer>(true) : null;
    }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (health != null) health.OnLivesChanged += HandleLivesChanged;
        if (twins != null) twins.OnTwinChanged += HandleStateChanged;
        if (invincibility != null)
        {
            invincibility.OnInvincibilityChanged += HandleStateChanged;
            invincibility.OnRainbowChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (health != null) health.OnLivesChanged -= HandleLivesChanged;
        if (twins != null) twins.OnTwinChanged -= HandleStateChanged;
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

        bool fullHealth = health.CurrentLives >= health.MaxLives && (twins == null || !twins.IsTwinned);
        bool invincible = invincibility != null && invincibility.IsInvincible;
        bool rainbow = invincibility != null && invincibility.RainbowShowing;

        bool shielded = fullHealth || invincible;
        Shielded = shielded;
        if (shellCollider != null) shellCollider.enabled = shielded;
        if (bodyCollider != null) bodyCollider.enabled = !shielded; // the shell covers the body, so only one collider can trigger

        if (shellMesh != null)
        {
            shellMesh.SetActive(rainbow || fullHealth);
            if (shellRenderer != null) shellRenderer.sharedMaterial = rainbow ? rainbowMaterial : shieldMaterial;
        }
        OnShellChanged?.Invoke();
    }
}
