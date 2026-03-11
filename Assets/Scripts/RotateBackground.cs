using UnityEngine;

public class RotateBackground : MonoBehaviour
{
    public float rotationSpeed = 5f; // Degrees per second

    void Update()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}