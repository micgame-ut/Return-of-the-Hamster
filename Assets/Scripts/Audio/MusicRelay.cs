using UnityEngine;

public class MusicRelay : MonoBehaviour
{
    public void StartGame()     => MusicManager.Instance.StartGame();
    public void StartCredits()  => MusicManager.Instance.StartCredits();
    public void ReturnToTitle() => MusicManager.Instance.ReturnToTitle();
    public void GoToTitle()     => MusicManager.Instance.GoToTitle();
}
