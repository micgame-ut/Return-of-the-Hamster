using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Add this to any UI Button to play sound effects when it is clicked (and optionally hovered).
///
/// Sound names must match the IDs in SFXManager's "Sounds" list.
/// "Click Sounds" can hold several IDs, and they all play at once (layered effects).
///
/// Because this lives on the button itself and looks up SFXManager.Instance when it is clicked,
/// it keeps working after the scene reloads, with no wiring to redo.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour, IPointerEnterHandler
{
    [Tooltip("IDs from SFXManager. All of them play when the button is clicked.")]
    [SerializeField] private string[] clickSounds = new[] { "click" };

    [Tooltip("Optional ID played when the pointer moves onto the button. Leave empty for none.")]
    [SerializeField] private string hoverSound = "";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        var sfx = SFXManager.Instance;
        if (sfx == null || clickSounds == null) return;

        foreach (var id in clickSounds)
            sfx.Play(id);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var sfx = SFXManager.Instance;
        if (sfx == null || string.IsNullOrEmpty(hoverSound)) return;
        if (button != null && !button.interactable) return;

        sfx.Play(hoverSound);
    }
}