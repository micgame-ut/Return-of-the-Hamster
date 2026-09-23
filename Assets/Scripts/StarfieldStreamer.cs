using UnityEngine;

// Spawns a wall of star particles ahead of the
// viewer and streams them past at a rate driven by PlayerMovement.Speed,
// giving the illusion of flying through the field even though the player

[DisallowMultipleComponent]
public class StarfieldStreamer : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float baseSpeed = 30f;
    [SerializeField] private float speedMultiplier = 15f;
    [SerializeField] private float sidewaysDrift = 2f;

    [Header("Field Shape")]
    [SerializeField] private float fieldWidth = 500f;
    [SerializeField] private float fieldHeight = 320f;
    [SerializeField] private float fieldDepth = 40f;
    [SerializeField] private float spawnDistance = 300f;
    [SerializeField] private float despawnDistance = -50f;

    [Header("Stars")]
    [SerializeField] private float emissionRate = 180f;
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private float minSize = 0.2f;
    [SerializeField] private float maxSize = 0.8f;
    [SerializeField] private Color colorA = Color.white;
    [SerializeField] private Color colorB = new Color(0.75f, 0.85f, 1f, 1f);
    [SerializeField] private float streakLengthScale = 2f;
    [SerializeField] private Material starMaterial;

    private ParticleSystem stars;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;

    private void Awake()
    {
        // Build the particle system once and cache its velocity module so
        // Update can retune the streaming speed every frame without any
        // per-frame allocation or lookup.
        stars = BuildParticleSystem();
        velocityModule = stars.velocityOverLifetime;
    }

    private void Update()
    {
        // Recompute forward streaming speed from the player's current speed
        // and push it into the cached velocity module.
        float speed = baseSpeed + PlayerMovement.Speed * speedMultiplier;
        velocityModule.z = new ParticleSystem.MinMaxCurve(-speed, -speed);
    }

    private ParticleSystem BuildParticleSystem()
    {
        // Create a child object under the camera so the particle system
        // rides along with it, rather than parenting stars to the camera
        // component directly.
        var go = new GameObject("StarfieldParticles");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        // Main module: lifetime, size, color, and cap on live particles.
        // Start speed is zero because all motion is driven by the
        // velocityOverLifetime module below instead.
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.85f, lifetime * 1.15f);
        main.startSpeed = 0f; // all motion comes from velocityOverLifetime below
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = 3000;

        // Emission: how many new stars spawn per second.
        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        // Shape: a box positioned out in front of the camera that stars
        // spawn within, giving the field its width/height/depth extent.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(fieldWidth, fieldHeight, fieldDepth);
        shape.position = new Vector3(0f, 0f, spawnDistance);

        // Velocity over lifetime: drives all actual particle motion. Z is
        // set to a placeholder here and overwritten every frame in Update
        // based on player speed; X/Y add a slight sideways drift for depth.
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        // All three axes must share the same MinMaxCurve mode (Unity throws
        // "Particle curves must all be in the same mode" otherwise), so Z
        // uses the same two-constant form as X/Y even though both are equal.
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-sidewaysDrift, sidewaysDrift);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-sidewaysDrift, sidewaysDrift);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-baseSpeed, -baseSpeed);

        // Color over lifetime: fades each star in and out so spawning and
        // despawning aren't visible as hard pops.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fade;

        // Renderer: stretches each particle along its velocity direction to
        // form a streak (like a warp-speed star trail) rather than a dot.
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.02f;
        renderer.lengthScale = streakLengthScale;
        renderer.material = starMaterial != null ? starMaterial : CreateFallbackStarMaterial();

        // Despawn distance only informs the field's effective travel range for tuning
        // (spawnDistance - despawnDistance); the actual cutoff is lifetime + fade above.
        return ps;
    }

    private Material CreateFallbackStarMaterial()
    {
        // Used only when no starMaterial is assigned in the inspector.
        // Try URP's unlit particle shader first, then legacy/built-in
        // equivalents, so this still works across render pipelines.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        // Apply Unity's built-in soft dot texture so particles render as
        // stars instead of flat squares.
        Texture2D dot = Resources.GetBuiltinResource<Texture2D>("Default-Particle.psd");
        if (dot != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", dot);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", dot);
        }
        return mat;
    }
}
