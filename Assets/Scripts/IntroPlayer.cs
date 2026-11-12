using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class IntroPlayer : MonoBehaviour
{
    [Tooltip("The VideoPlayer that plays the logo mp4. Assign the mp4 on this component.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("The AudioSource that plays the logo .wav. Assign the .wav as its clip, with Play On Awake off.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Name of the scene to load once both the video and the sound have finished. It must be in Build Settings.")]
    [SerializeField] private string nextSceneName = "Level_1";

    private AsyncOperation sceneLoad;
    private bool started;
    private bool finished;

    private void Start()
    {
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();

        sceneLoad = SceneManager.LoadSceneAsync(nextSceneName);
        sceneLoad.allowSceneActivation = false;
    }

    private void OnPrepared(VideoPlayer source)
    {
        videoPlayer.Play();
        audioSource.Play();
        started = true;
    }

    private void Update()
    {
        if (!started || finished) return;
        if (videoPlayer.isPlaying || audioSource.isPlaying) return;

        finished = true;
        sceneLoad.allowSceneActivation = true;
    }
}
