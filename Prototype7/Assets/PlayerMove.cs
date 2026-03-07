using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    private Rigidbody2D rb;

    void Start()
    {
        // Get the Rigidbody2D component once when the game starts
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Get input from Arrow keys or WASD
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // Apply movement to the Rigidbody
        rb.linearVelocity = new Vector2(moveX * speed, moveY * speed);
    }
}