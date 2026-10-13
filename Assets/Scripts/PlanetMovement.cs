using UnityEngine;

public class PlanetMovement : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 10f;
    private void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }
}
