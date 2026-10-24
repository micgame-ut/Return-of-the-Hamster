using UnityEngine;

public class LevelMusicStarter : MonoBehaviour
{
    private void Start()
    {
        var music = MusicManager.Instance;
        if (music != null && music.GameStarted)
        {
            music.PlayLevelMusic();
        }
    }
}