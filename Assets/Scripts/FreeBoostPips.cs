using UnityEngine;

// On the player and on each twin copy: shows one small ring for each free boost left from a Boost Recharge power-up.
public class FreeBoostPips : MonoBehaviour
{
    [Tooltip("The small rings. The first shows while at least 1 free boost is left, the second while at least 2 are. The last one in the list disappears first when a boost is used.")]
    [SerializeField] private GameObject[] pips;

    private PlayerMovement movement;
    private PlayerLance lance;
    private BoostRing ring;                 // the main ring beside the small ones, which knows the pink
    private MaterialPropertyBlock block;    // the pink, written to a small ring while Lance is active

    private void Awake()
    {
        // On the player, PlayerMovement is on a parent. A twin copy has none, so it follows the player through GameManager.
        movement = GetComponentInParent<PlayerMovement>();
        if (movement == null && GameManager.Instance != null) movement = GameManager.Instance.Player;
        lance = GameManager.Instance != null ? GameManager.Instance.PlayerLance : null;
        ring = GetComponentInChildren<BoostRing>(true);
        block = new MaterialPropertyBlock();
    }

    // Shows the first rings and hides the rest: one per free boost, or, while Lance is active, one per shot after the first.
    private void Refresh()
    {
        bool lanceOn = lance != null && lance.IsActive;
        int count = lanceOn ? lance.ShotsLeft - 1 : movement.FreeBoosts;
        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] == null) continue;
            pips[i].SetActive(i < count);
            Tint(pips[i], lanceOn);
        }
    }

    // Pink while Lance is active, the material's own color otherwise.
    private void Tint(GameObject pip, bool lanceOn)
    {
        Renderer r = pip.GetComponent<Renderer>();
        if (r == null) return;

        if (lanceOn && ring != null)
        {
            block.Clear();
            ring.WriteLanceColors(block);
            r.SetPropertyBlock(block);
        }
        else r.SetPropertyBlock(null);
    }

    private void OnEnable()
    {
        if (movement == null) return;
        movement.OnFreeBoostsChanged += Refresh;
        if (lance != null) lance.OnShotsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnFreeBoostsChanged -= Refresh;
        if (lance != null) lance.OnShotsChanged -= Refresh;
    }
}
