using TMPro;
using UnityEngine;

// The leaderboard window: the top scores as one block of text, with the row just added highlighted.
public class LeaderboardPanel : MonoBehaviour
{
    [Tooltip("The text the rows are written into, one line per place.")]
    [SerializeField] private TMP_Text rowsText;
    [Tooltip("The colour of the row just added. The other rows keep the text's own colour.")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("How many digits a score is padded to, so the scores line up. 6 gives 000123, as on the score total.")]
    [SerializeField, Range(1, 9)] private int scoreDigits = 6;

    private int highlight = -1;   // the place to highlight, 0 = first, -1 = none

    // Opens the window. Pass the new entry's place to highlight it, or -1 for no highlight.
    public void Show(int highlightRank)
    {
        highlight = highlightRank;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        highlight = -1;
        gameObject.SetActive(false);
    }

    private void OnEnable() => Refresh();

    // Writes one line per place, filling the places nobody has reached with dashes.
    private void Refresh()
    {
        string accent = ColorUtility.ToHtmlStringRGB(highlightColor);
        string empty = new string('0', scoreDigits);
        string text = "";

        for (int i = 0; i < LeaderboardStore.MaxEntries; i++)
        {
            bool taken = i < LeaderboardStore.Entries.Count;
            string name = taken ? LeaderboardStore.Entries[i].name : "---";
            string score = taken ? LeaderboardStore.Entries[i].score.ToString("D" + scoreDigits) : empty;

            // The <pos> tags start the name and the score at the same place on every line, so they form columns.
            string line = $"{i + 1}.<pos=18%>{name}<pos=45%>{score}";
            text += (i == highlight ? $"<color=#{accent}>{line}</color>" : line) + "\n";
        }

        rowsText.text = text;
    }
}
