using UnityEngine;
using System.Collections;

public class MenuTransition : MonoBehaviour
{
    [Tooltip("Seconds for the planets and UI to reach their targets.")]
    [SerializeField] private float duration = 1f;
    [Tooltip("Seconds for the hamster's glide. Longer than duration, so it arrives last.")]
    [SerializeField] private float hamsterDuration = 2.5f;
    [Tooltip("Seconds the hamster sits at its spot before the level starts.")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("Hamster")]
    [Tooltip("The player. The glide's target comes from its StartPosition and StartRotation.")]
    [SerializeField] private PlayerMovement hamster;

    [Header("Planets")]
    [Tooltip("The far planet.")]
    [SerializeField] private Transform farPlanet;
    [Tooltip("Where the far planet ends up (its gameplay spot).")]
    [SerializeField] private Transform farPlanetTarget;
    [Tooltip("The near planet.")]
    [SerializeField] private Transform nearPlanet;
    [Tooltip("The off-screen spot the near planet slides to.")]
    [SerializeField] private Transform nearPlanetTarget;

    [Header("UI")]
    [Tooltip("Everything that flies back past the camera.")]
    [SerializeField] private Transform[] uiElements;
    [Tooltip("How far behind the camera the UI finishes, in world units.")]
    [SerializeField] private float uiEndBehindCamera = 2f;

    private Camera cam;
    private HamsterMenuFloat hamsterFloat;
    private PlanetMovement nearSpin;

    public float Duration => duration;

    void Awake()
    {
        cam = Camera.main;
        hamsterFloat = hamster.GetComponent<HamsterMenuFloat>();
        nearSpin = nearPlanet.GetComponent<PlanetMovement>();

        // A restart skips the menu: the game has already started, so put the menu away at once.
        if (GameManager.Instance != null && GameManager.Instance.HasStarted)
        {
            hamsterFloat.enabled = false;
            farPlanet.SetPositionAndRotation(farPlanetTarget.position, farPlanetTarget.rotation);
            farPlanet.localScale = farPlanetTarget.localScale;
            HideMenu();
        }
    }

    private void HideMenu()
    {
        nearPlanet.gameObject.SetActive(false);
        foreach (Transform ui in uiElements) ui.gameObject.SetActive(false);
    }

    public void Play()
    {
        hamsterFloat.enabled = false;
        nearSpin.enabled = false;
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        Transform hamsterTransform = hamster.transform;
        Vector3 hamsterStartPos = hamsterTransform.position;
        Quaternion hamsterStartRot = hamsterTransform.rotation;
        Vector3 hamsterEndPos = hamster.StartPosition;
        Quaternion hamsterEndRot = hamster.StartRotation;
        Vector3 farStartPos = farPlanet.position;
        Quaternion farStartRot = farPlanet.rotation;
        Vector3 farStartScale = farPlanet.localScale;
        Vector3 nearStartPos = nearPlanet.position;

        float endZ = cam.transform.position.z - uiEndBehindCamera;
        Vector3[] uiStart = new Vector3[uiElements.Length];
        Vector3[] uiEnd = new Vector3[uiElements.Length];
        for (int i = 0; i < uiElements.Length; i++)
        {
            uiStart[i] = uiElements[i].position;
            uiEnd[i] = new Vector3(uiStart[i].x, uiStart[i].y, endZ);
        }

        float elapsed = 0f;
        float totalTime = Mathf.Max(duration, hamsterDuration);
        while (elapsed < totalTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(duration, 0.01f));
            float hamsterT = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(hamsterDuration, 0.01f));

            hamsterTransform.SetPositionAndRotation(Vector3.Lerp(hamsterStartPos, hamsterEndPos, hamsterT),
                                                    Quaternion.Slerp(hamsterStartRot, hamsterEndRot, hamsterT));
            farPlanet.SetPositionAndRotation(Vector3.Lerp(farStartPos, farPlanetTarget.position, t),
                                             Quaternion.Slerp(farStartRot, farPlanetTarget.rotation, t));
            farPlanet.localScale = Vector3.Lerp(farStartScale, farPlanetTarget.localScale, t);
            nearPlanet.position = Vector3.Lerp(nearStartPos, nearPlanetTarget.position, t);

            for (int i = 0; i < uiElements.Length; i++)
            {
                uiElements[i].position = Vector3.Lerp(uiStart[i], uiEnd[i], t);
            }

            yield return null;
        }

        HideMenu();
        yield return new WaitForSeconds(startDelay);
        GameManager.Instance.StartGame();
    }
}
