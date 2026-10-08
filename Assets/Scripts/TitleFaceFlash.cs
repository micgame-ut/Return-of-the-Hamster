using UnityEngine;
using System.Collections;

public class TitleFaceFlash : MonoBehaviour
{
    public Color yellow = Color.yellow;
    public Color white = Color.white;
    public Color blue = new Color(34f / 255f, 128f / 255f, 191f / 255f, 1f);

    public float yellowHold = .7f;
    public float whiteFlash = .12f;
    public float blueHold = .75f;
    public float pauseAfterFlash = 1.2f;

    private Renderer faceRenderer;
    private Material faceMaterial;

    void Awake()
    {
        faceRenderer = GetComponent<Renderer>(); // gets the renderer component
        faceMaterial = GetComponent<Renderer>().material; // gets the material from the renderer
    }

    void Start()
    {
        StartCoroutine(FlashLoop());
    }

    IEnumerator FlashLoop()
    {
        while (true)
        {
            // Long yellow hold
            faceMaterial.color = yellow;
            yield return new WaitForSeconds(yellowHold);

            // five fast white flashes over yellow
            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
            faceMaterial.color = blue;
            yield return new WaitForSeconds(whiteFlash);

            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
            faceMaterial.color = blue;
            yield return new WaitForSeconds(whiteFlash);

            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
            faceMaterial.color = blue;
            yield return new WaitForSeconds(whiteFlash);

            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
            faceMaterial.color = blue;
            yield return new WaitForSeconds(whiteFlash);

            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);

            // Blue hold
            faceMaterial.color = blue;
            yield return new WaitForSeconds(blueHold);

            // Three fast white flashes over blue
            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
            faceMaterial.color = blue;
            yield return new WaitForSeconds(whiteFlash);

            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
            faceMaterial.color = blue;
            yield return new WaitForSeconds(whiteFlash);

            faceMaterial.color = white;
            yield return new WaitForSeconds(whiteFlash);
        }
    }
}
