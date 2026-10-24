using UnityEngine;

public class PlanetMovement : MonoBehaviour
{
    [Tooltip("Degrees per second the planet spins around its Y axis.")]
    [SerializeField] private float rotationSpeed = 10f;
    private void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }
}
