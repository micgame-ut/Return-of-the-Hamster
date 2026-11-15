using UnityEngine;

// On the player and on each twin copy: shows one small ring for each free boost left from a Boost Recharge power-up.
public class FreeBoostPips : MonoBehaviour
{
    [Tooltip("The small rings. The first shows while at least 1 free boost is left, the second while at least 2 are. The last one in the list disappears first when a boost is used.")]
    [SerializeField] private GameObject[] pips;

    private PlayerMovement movement;

    private void Awake()
    {
        // On the player, PlayerMovement is on a parent. A twin copy has none, so it follows the player through GameManager.
        movement = GetComponentInParent<PlayerMovement>();
        if (movement == null && GameManager.Instance != null) movement = GameManager.Instance.Player;
    }

    // Shows the first FreeBoosts rings and hides the rest.
    private void Refresh()
    {
        for (int i = 0; i < pips.Length; i++)
            if (pips[i] != null) pips[i].SetActive(i < movement.FreeBoosts);
    }

    private void OnEnable()
    {
        if (movement == null) return;
        movement.OnFreeBoostsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnFreeBoostsChanged -= Refresh;
    }
}
