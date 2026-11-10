using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The three-letter name page, shown after a run that made the leaderboard. Rolling the ball down changes the
// current letter forward, up changes it back, the button locks it and moves on, and rolling left steps back a slot.
public class NameEntry : MonoBehaviour
{
    [Tooltip("The three texts that show the letters, left to right.")]
    [SerializeField] private TMP_Text[] letterSlots;
    [Tooltip("The colour of the letter being chosen.")]
    [SerializeField] private Color activeColor = Color.white;
    [Tooltip("The colour of the letters not being chosen.")]
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("The Move action (the mouse delta, which the trackball sends).")]
    [SerializeField] private InputActionReference moveAction;
    [Tooltip("The Button action (Space). Locks the current letter.")]
    [SerializeField] private InputActionReference selectAction;
    [Tooltip("Pixels of trackball movement that change a letter by one, or step back a slot. Lower = more sensitive.")]
    [SerializeField] private float stepDistance = 120f;
    [Tooltip("Movement smaller than this in one frame is ignored, so a drifting ball can't change anything.")]
    [SerializeField] private float deadzone = 2f;
    [Tooltip("Seconds after the page opens before it reacts to input, so the press that skipped the score count doesn't lock a letter.")]
    [SerializeField, Min(0f)] private float inputDelay = 0.5f;

    // Raised for sound: a letter changed, a letter was locked, and the whole name was submitted.
    public static event Action OnLetterChanged;
    public static event Action OnLetterLocked;
    public static event Action OnNameSubmitted;

    // Raised with the finished name, for whoever opened the page.
    public event Action<string> OnSubmitted;

    private int[] letters;       // each slot's letter, 0 = A to 25 = Z
    private int slot;            // the slot being chosen
    private float forward;       // downward movement so far toward the next letter (negative = toward the previous)
    private float backward;      // leftward movement so far toward stepping back a slot
    private float openedAt;      // the unscaled time the page opened, for inputDelay

    private void OnEnable()
    {
        letters = new int[letterSlots.Length];   // every slot starts at 0 = A
        slot = 0;
        forward = 0f;
        backward = 0f;
        openedAt = Time.unscaledTime;

        // Other scripts may have enabled these already; Enable() on an enabled action does nothing.
        if (moveAction != null) moveAction.action.Enable();
        if (selectAction != null) selectAction.action.Enable();

        Draw();
    }

    // Shows each slot's letter, with the one being chosen in the active colour.
    private void Draw()
    {
        for (int i = 0; i < letterSlots.Length; i++)
        {
            letterSlots[i].text = ((char)('A' + letters[i])).ToString();
            letterSlots[i].color = i == slot ? activeColor : inactiveColor;
        }
    }

    private const int LetterCount = 26;   // A to Z

    private void ChangeLetter(int direction)
    {
        // Adding LetterCount before the second % keeps -1 from going negative, so A wraps back to Z.
        letters[slot] = ((letters[slot] + direction) % LetterCount + LetterCount) % LetterCount;
        Draw();
        OnLetterChanged?.Invoke();
    }

    private void StepBack()
    {
        if (slot == 0) return;

        slot--;
        Draw();
        OnLetterChanged?.Invoke();
    }

    private void Update()
    {
        if (Time.unscaledTime < openedAt + inputDelay) return;

        Vector2 move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
        {
            // Sideways rolling steps back a slot, but only to the left. Rolling right, or a drift under the deadzone, clears what built up.
            backward = move.x <= -deadzone ? backward - move.x : 0f;
            if (backward >= stepDistance)
            {
                StepBack();
                backward = 0f;
            }
        }
        else
        {
            // Rolling down is negative y, so 'roll' is positive toward the next letter.
            float roll = -move.y;
            if (Mathf.Abs(roll) >= deadzone)
            {
                // Rolling the other way throws away what was built up toward the old direction.
                if (Mathf.Sign(roll) != Mathf.Sign(forward)) forward = 0f;
                forward += roll;

                if (Mathf.Abs(forward) >= stepDistance)
                {
                    ChangeLetter(forward > 0f ? 1 : -1);
                    forward = 0f;
                }
            }
        }

        if (selectAction != null && selectAction.action.WasPressedThisFrame()) LockLetter();
    }

    // Locks the current letter and moves to the next slot. Locking the last slot submits the name.
    private void LockLetter()
    {
        OnLetterLocked?.Invoke();

        if (slot < letterSlots.Length - 1)
        {
            slot++;
            Draw();
            return;
        }

        string name = "";
        foreach (int letter in letters) name += (char)('A' + letter);

        // Close first, so the page below is on top again before whoever listens opens the leaderboard over it.
        gameObject.SetActive(false);
        OnNameSubmitted?.Invoke();
        OnSubmitted?.Invoke(name);
    }
}
