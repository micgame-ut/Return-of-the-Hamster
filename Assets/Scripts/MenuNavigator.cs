using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Lets a page of buttons be used with only a trackball and one button: rolling the ball moves the selection
// and the button presses the selected item. Put one on each page (menu, credits, end screens).
public class MenuNavigator : MonoBehaviour
{
    public enum Layout { Vertical, Horizontal }

    [Tooltip("The page's buttons, in the order the selection moves through them.")]
    [SerializeField] private Button[] buttons;
    [Tooltip("Vertical: rolling the ball down goes to the next button. Horizontal: rolling it right goes to the next. The other direction is ignored.")]
    [SerializeField] private Layout layout = Layout.Vertical;
    [Tooltip("The Move action (the mouse delta, which the trackball sends).")]
    [SerializeField] private InputActionReference moveAction;
    [Tooltip("The Button action (Space). Presses the selected button.")]
    [SerializeField] private InputActionReference selectAction;
    [Tooltip("Pixels of trackball movement that move the selection by one button. Lower = more sensitive.")]
    [SerializeField] private float stepDistance = 60f;
    [Tooltip("Movement smaller than this in one frame is ignored, so a drifting ball can't move the selection.")]
    [SerializeField] private float deadzone = 2f;
    [Tooltip("Moving past the last button goes to the first, and the other way round.")]
    [SerializeField] private bool wrapAround = true;

    [Header("Marker")]
    [Tooltip("A triangle (UI Image) shown beside the selected button. Empty = no marker. Make it a direct child of the page, not inside a Layout Group, or the group will move it.")]
    [SerializeField] private RectTransform marker;
    [Tooltip("Where the marker sits, in canvas units, from the middle of the selected button's left edge. A negative x puts it further left.")]
    [SerializeField] private Vector2 markerOffset = new Vector2(-50f, 0f);

    private int selectedIndex;     // which button is selected now
    private float accumulated;     // movement so far toward the next step; its sign is the direction

    // Every page that is showing, oldest first. Only the last one (the page on top) reacts to the trackball.
    private static readonly List<MenuNavigator> openPages = new List<MenuNavigator>();

    private bool IsTopPage => openPages.Count > 0 && openPages[openPages.Count - 1] == this;

    private void OnEnable()
    {
        openPages.Add(this);
        accumulated = 0f;

        // Other scripts may have enabled these already; Enable() on an enabled action does nothing.
        if (moveAction != null) moveAction.action.Enable();
        if (selectAction != null) selectAction.action.Enable();

        // There is no mouse pointer in this game: hide it and keep it in the middle of the screen.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (buttons.Length > 0)
            Select(buttons[0] != null && buttons[0].interactable ? 0 : FindUsable(0, 1));
    }

    private void OnDisable()
    {
        openPages.Remove(this);

        // The page below (if there is one) is on top again, so highlight its button again.
        if (openPages.Count > 0)
        {
            MenuNavigator below = openPages[openPages.Count - 1];
            below.Select(below.selectedIndex);
        }
    }

    private void Update()
    {
        // Only the page on top reacts; a page with no buttons has nothing to select.
        if (!IsTopPage || buttons.Length == 0) return;

        // Only the axis that matches the layout counts. Rolling down is negative y, rolling right is positive x.
        // 'roll' is positive when the ball moves toward the next button.
        Vector2 move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        float roll = layout == Layout.Vertical ? -move.y : move.x;

        if (Mathf.Abs(roll) >= deadzone)
        {
            // Rolling the other way throws away what was built up toward the old direction.
            if (Mathf.Sign(roll) != Mathf.Sign(accumulated)) accumulated = 0f;
            accumulated += roll;

            if (Mathf.Abs(accumulated) >= stepDistance)
            {
                Select(FindUsable(selectedIndex, accumulated > 0f ? 1 : -1));
                accumulated = 0f;
            }
        }

        // Space presses the selected button the way Enter would, so it also plays the button's press look.
        if (selectAction != null && selectAction.action.WasPressedThisFrame() && buttons[selectedIndex] != null)
            ExecuteEvents.Execute(buttons[selectedIndex].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }

    // Makes the button at this index the selected one: the EventSystem highlights it with the Button's Selected look.
    private void Select(int index)
    {
        selectedIndex = index;
        if (EventSystem.current != null && buttons[index] != null)
            EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);

        // A page that was just switched on hasn't been laid out yet, so do that first.
        Canvas.ForceUpdateCanvases();
        PlaceMarker();
    }

    // Moves the marker to the left-middle edge of the selected button, plus markerOffset.
    private void PlaceMarker()
    {
        if (marker == null || buttons.Length == 0 || buttons[selectedIndex] == null) return;

        // GetWorldCorners fills 4 corners: 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right.
        Vector3[] corners = new Vector3[4];
        ((RectTransform)buttons[selectedIndex].transform).GetWorldCorners(corners);
        marker.position = (corners[0] + corners[1]) * 0.5f;
        marker.anchoredPosition += markerOffset;
    }

    private void LateUpdate()
    {
        if (IsTopPage) PlaceMarker();
    }

    // Finds the next button that can be used, starting after 'from' and going 'direction' (+1 or -1).
    // Returns 'from' if there isn't one, so the selection stays put.
    private int FindUsable(int from, int direction)
    {
        for (int i = 1; i <= buttons.Length; i++)
        {
            int index = from + direction * i;
            if (wrapAround) index = (index % buttons.Length + buttons.Length) % buttons.Length;
            else if (index < 0 || index >= buttons.Length) break;

            if (buttons[index] != null && buttons[index].interactable && buttons[index].gameObject.activeInHierarchy)
                return index;
        }
        return from;
    }
}
