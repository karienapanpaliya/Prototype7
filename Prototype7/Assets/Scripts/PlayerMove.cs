using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    public float horizontalSpeed = 5f;
    public float verticalControlSpeed = 2.5f;
    public float baseUpwardSpeed = 2f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isDead;

    void Awake()
    {
        // Cache Rigidbody2D for physics movement.
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetDead()
    {
        isDead = true;
        moveInput = Vector2.zero;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void Update()
    {
        if (isDead)
        {
            moveInput = Vector2.zero;
            return;
        }

        // Read movement from keyboard (WASD/Arrows) and gamepad left stick.
        Vector2 keyboardInput = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keyboardInput.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keyboardInput.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keyboardInput.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keyboardInput.y += 1f;
        }

        Vector2 gamepadInput = Vector2.zero;
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepadInput = gamepad.leftStick.ReadValue();
        }

        // Combine and clamp player steering input.
        moveInput = Vector2.ClampMagnitude(keyboardInput + gamepadInput, 1f);
    }

    void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float velocityX = moveInput.x * horizontalSpeed;
        float velocityY = baseUpwardSpeed + (moveInput.y * verticalControlSpeed);

        // Keep upward progression so the run always advances.
        velocityY = Mathf.Max(0.25f, velocityY);

        rb.linearVelocity = new Vector2(velocityX, velocityY);
    }
}