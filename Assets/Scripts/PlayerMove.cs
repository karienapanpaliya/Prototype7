using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    public float horizontalSpeed = 35f;
    public float verticalControlSpeed = 28f;
    public float baseUpwardSpeed = 2f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isDead;
    private bool isPaused;
    private InputAction moveAction;

    void Awake()
    {
        // Cache Rigidbody2D for physics movement.
        rb = GetComponent<Rigidbody2D>();

        // Interpolate so LateUpdate camera reads a smoothed position between physics steps,
        // preventing the camera shake caused by mismatched FixedUpdate / render framerates.
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        moveAction = new InputAction("Move", InputActionType.Value);

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        // Deadzone helps avoid stick drift interfering with keyboard input.
        moveAction.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.2,max=0.95)");
        moveAction.Enable();
    }

    void OnDestroy()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
            moveAction.Dispose();
        }
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

    public void SetPaused(bool state)
    {
        isPaused = state;
        if (isPaused)
        {
            moveInput = Vector2.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    void Update()
    {
        if (isDead)
        {
            moveInput = Vector2.zero;
            return;
        }

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        
        if (isPaused)
        {
            // When paused, only allow vertical input to resume movement
            if (input.y > 0f)
            {
                isPaused = false;
            }
            else
            {
                moveInput = Vector2.zero;
                return;
            }
        }
        
        moveInput = Vector2.ClampMagnitude(input, 1f);
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

        if (isPaused)
        {
            velocityX = 0f;
            velocityY = 0f;
        }

        rb.linearVelocity = new Vector2(velocityX, velocityY);
    }
}