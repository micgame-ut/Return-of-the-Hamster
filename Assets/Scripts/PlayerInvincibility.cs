using UnityEngine;

// Timed invincibility from a power-up. While active, asteroids are busted on
// contact and a rainbow overlay material is added to the ship's renderers.
public class PlayerInvincibility : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;
    [SerializeField] private Material rainbowMaterial;
    [SerializeField] private float warningTime = 1f;
    [SerializeField] private float warningBlinkInterval = 0.1f;

    public bool IsInvincible => timer > 0f;

    private float timer;
    private Renderer[] meshRenderers;
    private Material[][] originalMaterials;
    private Material[][] rainbowMaterials;
    private bool overlayOn;

    private void Awake()
    {
        meshRenderers = meshTransform != null
            ? meshTransform.GetComponentsInChildren<Renderer>()
            : GetComponentsInChildren<Renderer>();

        originalMaterials = new Material[meshRenderers.Length][];
        rainbowMaterials = new Material[meshRenderers.Length][];
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            Material[] original = meshRenderers[i].sharedMaterials;
            originalMaterials[i] = original;

            Material[] withRainbow = new Material[original.Length + 1];
            original.CopyTo(withRainbow, 0);
            withRainbow[original.Length] = rainbowMaterial;
            rainbowMaterials[i] = withRainbow;
        }
    }

    private void SetOverlay(bool on)
    {
        if (on == overlayOn || rainbowMaterial == null) return;
        overlayOn = on;

        for (int i = 0; i < meshRenderers.Length; i++)
            meshRenderers[i].sharedMaterials = on ? rainbowMaterials[i] : originalMaterials[i];
    }

    public void Activate(float duration)
    {
        timer = duration;
        SetOverlay(true);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGameOver || gm.HasWon))
        {
            timer = 0f;
            SetOverlay(false);
            return;
        }

        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SetOverlay(false);
            return;
        }

        bool visible = timer > warningTime ||
                       Mathf.FloorToInt(timer / warningBlinkInterval) % 2 == 0;
        SetOverlay(visible);
    }
}
