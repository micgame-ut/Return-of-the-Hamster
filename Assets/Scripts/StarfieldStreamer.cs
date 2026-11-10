using UnityEngine;

// Spawns a wall of star particles ahead of the
// viewer and streams them past at a rate driven by PlayerMovement.Speed,
// giving the illusion of flying through the field

[DisallowMultipleComponent]
public class StarfieldStreamer : MonoBehaviour
{
    [Header("Speed")]
    [Tooltip("How fast stars stream past when the player's Speed is 0, in units/sec.")]
    [SerializeField] private float baseSpeed = 30f;
    [Tooltip("How much faster they go for each point of player Speed.")]
    [SerializeField] private float speedMultiplier = 15f;
    [Tooltip("Each star also drifts outward (away from the middle) at a random speed up to this, so they don't all move in perfect parallel lines.")]
    [SerializeField] private float sidewaysDrift = 2f;
    [Tooltip("Seconds for the stars to speed up from still to normal once the game starts.")]
    [SerializeField] private float rampTime = 1f;

    [Header("Boost")]
    [Tooltip("Extra star speed, in units/sec, the instant a forward boost fires. It fades back to normal over Boost Burst Time.")]
    [SerializeField] private float boostBurstSpeed = 120f;
    [Tooltip("Seconds the star burst takes to fade out after a forward boost. 0 = no burst.")]
    [SerializeField] private float boostBurstTime = 0.5f;

    [Header("Field Shape")]
    [Tooltip("Stars spawn up to this far from the middle...")]
    [SerializeField] private float fieldRadius = 300f;
    [Tooltip("...and no closer than this, so no star streams past the camera through the middle of the screen. Tune by eye.")]
    [SerializeField] private float clearRadius = 20f;
    [Tooltip("How far ahead of the camera the spawn ring is.")]
    [SerializeField] private float spawnDistance = 300f;
    [Tooltip("Stars are removed once they're this far behind the camera (the despawn plane).")]
    [SerializeField] private float despawnDistance = -50f;

    [Header("Stars")]
    [Tooltip("New stars per second.")]
    [SerializeField] private float emissionRate = 180f;
    [Tooltip("Roughly how long each star lasts, in seconds (each gets a random 85% to 115% of this).")]
    [SerializeField] private float lifetime = 8f;
    [Tooltip("Each star gets a random size between minSize and maxSize.")]
    [SerializeField] private float minSize = 0.2f;
    [Tooltip("Each star gets a random size between minSize and maxSize.")]
    [SerializeField] private float maxSize = 0.8f;
    [Tooltip("Each star gets a random color between colorA and colorB.")]
    [SerializeField] private Color colorA = Color.white;
    [Tooltip("Each star gets a random color between colorA and colorB.")]
    [SerializeField] private Color colorB = new Color(0.75f, 0.85f, 1f, 1f);
    [Tooltip("How long the streaks are.")]
    [SerializeField] private float streakLengthScale = 2f;
    [Tooltip("Material for the stars. If empty, one is made in code.")]
    [SerializeField] private Material starMaterial;

    private ParticleSystem stars;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;
    private ParticleSystem.EmissionModule emission;
    private bool emitting = true; // last value given to emission.enabled
    private PlayerMovement movement;
    private float burstTimer; // seconds left on the boost burst; 0 = none
    private Material fallbackMaterial;

    private void Awake()
    {
        // Build the particle system once and cache its velocity module so
        // Update can retune the streaming speed every frame without any
        // per-frame allocation or lookup.
        stars = BuildParticleSystem();
        velocityModule = stars.velocityOverLifetime;
        emission = stars.emission;
        movement = GameManager.Instance != null ? GameManager.Instance.Player : null;
    }

    private void OnEnable()
    {
        if (movement != null) movement.OnSpeedBoost += StartBurst;
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnSpeedBoost -= StartBurst;
    }

    private void StartBurst() => burstTimer = boostBurstTime;

    private void Update()
    {
        // No stars spawn on the menu. Once the game starts they spawn again and speed up from still over rampTime.
        GameManager gm = GameManager.Instance;
        bool started = gm == null || gm.IsFlying;
        if (started != emitting)
        {
            emission.enabled = started;
            emitting = started;
        }
        float ramp = gm == null ? 1f : Mathf.SmoothStep(0f, 1f, gm.TimeSinceStart / Mathf.Max(rampTime, 0.01f));

        // Recompute forward streaming speed from the player's current speed
        // and push it into the cached velocity module.
        float burst = 0f;
        if (burstTimer > 0f)
        {
            burstTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(burstTimer / boostBurstTime);
            burst = boostBurstSpeed * t * t; // squared, so the rush is sharp and fades fast
        }
        float speed = (baseSpeed + PlayerMovement.Speed * speedMultiplier + burst) * ramp;
        velocityModule.z = new ParticleSystem.MinMaxCurve(-speed, -speed);
    }

    private ParticleSystem BuildParticleSystem()
    {
        // Create a child object under the camera so the particle system
        // rides along with it, rather than parenting stars to the camera
        // component directly.
        var go = new GameObject("StarfieldParticles");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        // Main module: lifetime, size, color, and cap on live particles.
        // Start speed is a small outward drift (the circle fires particles away
        // from its center); forward motion comes from the velocityOverLifetime
        // module below.
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.85f, lifetime * 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, sidewaysDrift); // outward drift; forward motion is velocityOverLifetime below
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = 3000;

        // Emission: how many new stars spawn per second.
        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        // Shape: a ring out in front of the camera that stars spawn within. The
        // middle (inside clearRadius) is left empty, so no star streams past the
        // camera through the middle of the screen.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = fieldRadius;
        shape.radiusThickness = 1f - Mathf.Clamp01(clearRadius / fieldRadius);
        shape.arc = 360f;
        shape.position = new Vector3(0f, 0f, spawnDistance);

        // Velocity over lifetime: drives all actual particle motion. Z is
        // set to a placeholder here and overwritten every frame in Update
        // based on player speed; X/Y stay 0: the sideways drift is the outward
        // start speed instead, so it never heads into the empty middle.
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;

        // All three axes must share the same MinMaxCurve mode (Unity throws
        // "Particle curves must all be in the same mode" otherwise), so Z
        // uses the same two-constant form as X/Y even though both are equal.
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-baseSpeed, -baseSpeed);

        // Collision: an invisible plane despawnDistance behind the camera. A star
        // that touches it loses all its lifetime, so fast stars' long streak tails
        // never poke back into view. The particle system does this itself, so no
        // star is ever copied into C#.
        Transform despawnPlane = new GameObject("Star Despawn Plane").transform;
        despawnPlane.SetParent(go.transform, false);
        despawnPlane.localPosition = new Vector3(0f, 0f, despawnDistance);
        despawnPlane.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward); // a plane faces along its up axis: point it at the incoming stars

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.AddPlane(despawnPlane);
        collision.lifetimeLoss = 1f;
        collision.bounce = 0f;

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

        // Stars die at the end of their lifetime, or earlier at the despawn
        // plane (see Collision above).
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
        fallbackMaterial = mat;
        return mat;
    }

    private void OnDestroy()
    {
        if (fallbackMaterial != null) Destroy(fallbackMaterial);
    }
}
