using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    PlayerInput playerInput;
    PlayerInput.MainActions input;
    CharacterController controller;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.8f;
    public float jumpHeight = 1.2f;

    [Header("Camera")]
    public Camera cam;
    public float sensitivity = 100f;

    [Header("Combat Physics")]
    public Transform swordAnchor; // The ghost target childed to camera
    public Rigidbody swordRb;     // The actual sword RB
    public float windUpLimit = 10f; // Mouse distance required to "charge"
    public float swingForce = 40f;
    public float returnSpeed = 5f;

    private Vector3 _PlayerVelocity;
    private bool isGrounded;
    private float xRotation = 0f;

    // Combat State Tracking
    private bool isCombatMode = false;
    private Vector2 accumulatedMouseDelta;
    private bool hasReachedWindUp = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Initialize Input System
        playerInput = new PlayerInput();
        input = playerInput.Main;

        // Lock Cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Assign Jump Event
        input.Jump.performed += ctx => Jump();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;

        // Toggle Combat Mode based on Left Click
        isCombatMode = Mouse.current.leftButton.isPressed;

        if (!isCombatMode)
        {
            ResetSwordPosition();
        }
    }

    void FixedUpdate()
    {
        MoveInput(input.Movement.ReadValue<Vector2>());
    }

    void LateUpdate()
    {
        Vector2 lookValue = input.Look.ReadValue<Vector2>();

        if (isCombatMode)
        {
            // Lock camera looking and move the sword instead
            HandleSwordCombat(lookValue);
        }
        else
        {
            // Default first-person looking
            LookInput(lookValue);
        }
    }

    // --- MOVEMENT LOGIC ---

    void MoveInput(Vector2 moveInput)
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * moveSpeed * Time.deltaTime);

        if (isGrounded && _PlayerVelocity.y < 0)
        {
            _PlayerVelocity.y = -2f;
        }

        _PlayerVelocity.y += gravity * Time.deltaTime;
        controller.Move(_PlayerVelocity * Time.deltaTime);
    }

    void LookInput(Vector2 lookInput)
    {
        float mouseX = lookInput.x * sensitivity * Time.deltaTime;
        float mouseY = lookInput.y * sensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        transform.Rotate(Vector3.up * mouseX);
    }

    void Jump()
    {
        if (isGrounded)
        {
            _PlayerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }
    }

    // --- COMBAT LOGIC ---

    void HandleSwordCombat(Vector2 mouseDelta)
    {
        // 1. Accumulate mouse movement while holding click
        accumulatedMouseDelta += mouseDelta * (sensitivity / 100f);

        // Clamp it so the wind-up doesn't go behind the player's head
        accumulatedMouseDelta.x = Mathf.Clamp(accumulatedMouseDelta.x, -windUpLimit * 1.5f, windUpLimit * 1.5f);
        accumulatedMouseDelta.y = Mathf.Clamp(accumulatedMouseDelta.y, -windUpLimit * 1.5f, windUpLimit * 1.5f);

        // 2. Visual Wind-up: Rotate the anchor so the sword tip follows mouse direction
        // Pitch (X) is controlled by Mouse Y, Yaw (Y) is controlled by Mouse X
        Quaternion targetWindUp = Quaternion.Euler(-accumulatedMouseDelta.y * 2, accumulatedMouseDelta.x * 2, 0);
        swordAnchor.localRotation = Quaternion.Slerp(swordAnchor.localRotation, targetWindUp, Time.deltaTime * 15f);

        // 3. Detect "Swipe Back"
        // First check if we've moved the mouse far enough to count as a "wind up"
        if (accumulatedMouseDelta.magnitude > windUpLimit)
        {
            hasReachedWindUp = true;
        }

        if (hasReachedWindUp)
        {
            // Dot Product check: is the current mouse frame moving OPPOSITE to the total wind-up?
            float directionMatch = Vector2.Dot(mouseDelta.normalized, accumulatedMouseDelta.normalized);

            // If moving opposite (-0.5 or less) and with enough speed
            if (directionMatch < -0.5f && mouseDelta.magnitude > 2f)
            {
                ExecuteSwing(mouseDelta);
            }
        }
    }

    void ExecuteSwing(Vector2 swingDirection)
    {
        // Calculate a world-space direction based on where the anchor is pointing
        // We push the sword in the direction of the "swipe back"
        Vector3 forceDir = cam.transform.TransformDirection(new Vector3(swingDirection.x, swingDirection.y, 1f));

        swordRb.AddForce(forceDir * swingForce, ForceMode.Impulse);

        // Add some torque for that "swinging arc" feel
        Vector3 torqueDir = new Vector3(-swingDirection.y, swingDirection.x, 0);
        swordRb.AddRelativeTorque(torqueDir * swingForce, ForceMode.Impulse);

        // Reset tracking so we can swing again or wind up again
        accumulatedMouseDelta = Vector2.zero;
        hasReachedWindUp = false;
    }

    void ResetSwordPosition()
    {
        // Smoothly bring the anchor back to center and zero out delta
        swordAnchor.localRotation = Quaternion.Slerp(swordAnchor.localRotation, Quaternion.identity, Time.deltaTime * returnSpeed);
        accumulatedMouseDelta = Vector2.MoveTowards(accumulatedMouseDelta, Vector2.zero, Time.deltaTime * sensitivity);
        hasReachedWindUp = false;
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();
}