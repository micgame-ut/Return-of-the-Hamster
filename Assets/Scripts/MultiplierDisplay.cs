using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the score power-ups' part of the multiplier ("x4") with a bar that drains
// as the next one runs out. Hidden while none are active.
public class MultiplierDisplay : MonoBehaviour
{
    [Tooltip("The label and bar's parent. Switched on and off as a whole, so it's hidden while no power-ups are active.")]
    [SerializeField] private GameObject indicator;
    [Tooltip("Shows the power-up multiplier, for example x4.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("An Image with Image Type set to Filled. Its fillAmount drains from 1 to 0.")]
    [SerializeField] private Image bar;

    private Score score;
    private int shownCount = -1;

    private void Awake()
    {
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;

        // Start hidden, so the HUD appearing at the start never shows it for a frame before Update runs.
        if (indicator != null) indicator.SetActive(false);
    }

    private void Update()
    {
        int count = score != null ? score.ActivePowerUps : 0;
        if (count != shownCount)
        {
            shownCount = count;
            if (indicator != null) indicator.SetActive(count > 0);
            if (label != null && count > 0) label.text = $"x{score.PowerUpMultiplier:0.#}";
        }

        if (bar != null && count > 0) bar.fillAmount = score.NextPowerUpFraction;
    }
}
