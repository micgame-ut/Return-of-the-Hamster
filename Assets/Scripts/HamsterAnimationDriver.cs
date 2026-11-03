using UnityEngine;

// Drives the hamster's Animator from the player's speed, and plays the hit reaction when a life is lost.
public class HamsterAnimationDriver : MonoBehaviour
{
    [Tooltip("Playback rate of the Locomotion state when the player is stopped (Speed01 = 0). 1 = the clip's normal speed.")]
    [SerializeField] private float minRate = 0.8f;

    [Tooltip("Playback rate at the player's top speed (Speed01 = 1). 2 = twice the clip's normal speed.")]
    [SerializeField] private float maxRate = 2f;

    [Tooltip("Seconds the Animator takes to catch up when the player speeds up. 0 = instant, bigger = softer.")]
    [SerializeField] private float speedUpDampTime = 0.05f;

    [Tooltip("Seconds the Animator takes to wind down when the player slows. 0 = instant, bigger = a longer, gentler wind-down.")]
    [SerializeField] private float slowDownDampTime = 0.6f;

    private Animator animator;
    private PlayerHealth playerHealth;

    private static readonly int Speed01Param = Animator.StringToHash("Speed01");
    private static readonly int RunSpeedParam = Animator.StringToHash("RunSpeed");
    private static readonly int HitParam = Animator.StringToHash("Hit");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnLifeLost += HandleLifeLost;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnLifeLost -= HandleLifeLost;
    }

    private void HandleLifeLost() => animator.SetTrigger(HitParam);

    private void Update()
    {
        float speed01 = PlayerMovement.Speed01;
        bool speedingUp = speed01 > animator.GetFloat(Speed01Param);
        float damp = speedingUp ? speedUpDampTime : slowDownDampTime;
        animator.SetFloat(Speed01Param, speed01, damp, Time.deltaTime);
        animator.SetFloat(RunSpeedParam, Mathf.Lerp(minRate, maxRate, speed01), damp, Time.deltaTime);
    }
}
