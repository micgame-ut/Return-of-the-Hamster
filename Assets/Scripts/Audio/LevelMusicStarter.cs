using System.Collections;
using UnityEngine;

public class LevelMusicStarter : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;   // let MusicManager.Start finish first

        var music = MusicManager.Instance;
        if (music == null) yield break;

        // A level with no menu has no Start button, so it starts the music itself.
        if (!music.GameStarted && GameManager.Instance != null && GameManager.Instance.HasStarted)
            music.StartGame();
        else if (music.GameStarted)
            music.PlayLevelMusic();
    }
}