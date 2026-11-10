using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Plays a sound for each UI event. Every slot takes clips by drag and drop, and an empty slot plays nothing.
// Sits on the AudioManager next to GameSFX and plays through it.
[DisallowMultipleComponent]
public class UISFX : MonoBehaviour
{
    [Header("Menu")]
    [Tooltip("The selection moves to another button.")]
    [SerializeField] private GameSFX.Slot menuMove;
    [Tooltip("A button becomes highlighted: on a move, when a page opens, and when returning to a page below. Plays together with Menu Move when the selection moves.")]
    [SerializeField] private GameSFX.Slot buttonHover;
    [Tooltip("A button is pressed.")]
    [SerializeField] private GameSFX.Slot menuSelect;
    [Tooltip("A press lands on a button that can't be pressed.")]
    [SerializeField] private GameSFX.Slot buttonBlocked;
    [Tooltip("Start is pressed, before the transition. Plays together with Menu Select.")]
    [SerializeField] private GameSFX.Slot startPressed;

    [Header("Credits")]
    [Tooltip("The credits panel opens.")]
    [SerializeField] private GameSFX.Slot creditsOpen;
    [Tooltip("The credits panel closes when the credits finish.")]
    [SerializeField] private GameSFX.Slot creditsClose;

    [Header("End screen")]
    [Tooltip("The Game Over or Win screen appears.")]
    [SerializeField] private GameSFX.Slot endScreenShown;
    [Tooltip("The leaderboard opens.")]
    [FormerlySerializedAs("statsOpen")]
    [SerializeField] private GameSFX.Slot leaderboardOpen;
    [Tooltip("The leaderboard closes.")]
    [FormerlySerializedAs("statsClose")]
    [SerializeField] private GameSFX.Slot leaderboardClose;
    [Tooltip("A name letter changes, or steps back a slot.")]
    [SerializeField] private GameSFX.Slot letterChange;
    [Tooltip("A name letter is locked in.")]
    [SerializeField] private GameSFX.Slot letterLock;
    [Tooltip("The three-letter name is submitted. Plays together with Letter Lock.")]
    [SerializeField] private GameSFX.Slot nameSubmitted;
    [Tooltip("Each score row counts into the total.")]
    [SerializeField] private GameSFX.Slot scoreRow;
    [Tooltip("The score breakdown finishes.")]
    [SerializeField] private GameSFX.Slot scoreTotal;

    [Header("In-game")]
    [Tooltip("A score popup appears. This happens often, so leave it empty or keep it quiet.")]
    [SerializeField] private GameSFX.Slot scorePopup;

    private static UISFX instance;
    private Score score;   // this level's Score; Bind finds it again each time a level loads

    private void Awake()
    {
        // A reloaded Level 1 brings a second AudioManager; only the first UISFX stays.
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnEnable()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        MenuNavigator.OnMoved += HandleMoved;
        MenuNavigator.OnHighlighted += HandleHighlighted;
        MenuNavigator.OnPressed += HandlePressed;
        MenuNavigator.OnBlocked += HandleBlocked;
        MainMenu.OnStartClicked += HandleStartClicked;
        CreditsPanel.OnOpened += HandleCreditsOpened;
        CreditsPanel.OnClosed += HandleCreditsClosed;
        EndScreen.OnShown += HandleEndScreenShown;
        EndScreen.OnLeaderboardOpened += HandleLeaderboardOpened;
        EndScreen.OnLeaderboardClosed += HandleLeaderboardClosed;
        NameEntry.OnLetterChanged += HandleLetterChanged;
        NameEntry.OnLetterLocked += HandleLetterLocked;
        NameEntry.OnNameSubmitted += HandleNameSubmitted;
        ScoreBreakdown.OnRowCounted += HandleRowCounted;
        ScoreBreakdown.OnFinished += HandleFinished;
        Bind();
    }

    private void OnDisable()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        MenuNavigator.OnMoved -= HandleMoved;
        MenuNavigator.OnHighlighted -= HandleHighlighted;
        MenuNavigator.OnPressed -= HandlePressed;
        MenuNavigator.OnBlocked -= HandleBlocked;
        MainMenu.OnStartClicked -= HandleStartClicked;
        CreditsPanel.OnOpened -= HandleCreditsOpened;
        CreditsPanel.OnClosed -= HandleCreditsClosed;
        EndScreen.OnShown -= HandleEndScreenShown;
        EndScreen.OnLeaderboardOpened -= HandleLeaderboardOpened;
        EndScreen.OnLeaderboardClosed -= HandleLeaderboardClosed;
        NameEntry.OnLetterChanged -= HandleLetterChanged;
        NameEntry.OnLetterLocked -= HandleLetterLocked;
        NameEntry.OnNameSubmitted -= HandleNameSubmitted;
        ScoreBreakdown.OnRowCounted -= HandleRowCounted;
        ScoreBreakdown.OnFinished -= HandleFinished;
        Unbind();
    }

    private void HandleMoved() => GameSFX.Play(menuMove);
    private void HandleHighlighted() => GameSFX.Play(buttonHover);
    private void HandlePressed() => GameSFX.Play(menuSelect);
    private void HandleBlocked() => GameSFX.Play(buttonBlocked);
    private void HandleStartClicked() => GameSFX.Play(startPressed);
    private void HandleCreditsOpened() => GameSFX.Play(creditsOpen);
    private void HandleCreditsClosed() => GameSFX.Play(creditsClose);
    private void HandleEndScreenShown() => GameSFX.Play(endScreenShown);
    private void HandleLeaderboardOpened() => GameSFX.Play(leaderboardOpen);
    private void HandleLeaderboardClosed() => GameSFX.Play(leaderboardClose);
    private void HandleLetterChanged() => GameSFX.Play(letterChange);
    private void HandleLetterLocked() => GameSFX.Play(letterLock);
    private void HandleNameSubmitted() => GameSFX.Play(nameSubmitted);
    private void HandleRowCounted() => GameSFX.Play(scoreRow);
    private void HandleFinished() => GameSFX.Play(scoreTotal);

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Bind();

    // The event hands over more than a sound needs; only the fact that a popup appeared matters here.
    private void HandlePointsAdded(float points, float multiplier, Vector3 position, string label) => GameSFX.Play(scorePopup);

    private void Bind()
    {
        Unbind();
        GameManager gm = GameManager.Instance;
        score = gm != null ? gm.Score : null;
        if (score != null) score.OnPointsAdded += HandlePointsAdded;
    }

    private void Unbind()
    {
        if (score != null) score.OnPointsAdded -= HandlePointsAdded;
    }
}
