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
    public Transform swordAnchor;
    public Rigidbody swordRb;
    public float windUpThreshold = 3f;
    public float swingSensitivity = 5.0f;
    public float maxSwingForce = 400f;
    public float returnSpeed = 25f;

    private Vector3 _PlayerVelocity;
    private bool isGrounded;
    private float xRotation = 0f;

    // Combat State
    private Vector2 accumulatedDelta;
    private bool isCombatMode = false;
    private bool isSwinging = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = new PlayerInput();
        input = playerInput.Main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        input.Jump.performed += ctx => Jump();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        isCombatMode = Mouse.current.leftButton.isPressed;

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            ApplySwordBrake();
        }
    }

    void FixedUpdate() => MoveInput(input.Movement.ReadValue<Vector2>());

    void LateUpdate()
    {
        Vector2 lookInput = input.Look.ReadValue<Vector2>();

        if (isCombatMode)
        {
            HandleSwordCombat(lookInput);
        }
        else
        {
            LookInput(lookInput);
            ResetAnchorSmoothly();
        }
    }

    // --- COMBAT LOGIC ---

    void HandleSwordCombat(Vector2 mouseDelta)
    {
        if (!isSwinging)
        {
            // 1. Accumulate movement
            accumulatedDelta += mouseDelta * (sensitivity / 100f);

            // 2. Calculate Angle for the Red Axis Normal Plane (Y-Z Plane)
            if (accumulatedDelta.magnitude > 0.1f)
            {
                // Atan2 gives the angle in radians between the X-axis and the vector (y, x)
                // We use this to find the direction the mouse has moved relative to center
                float mouseAngle = Mathf.Atan2(accumulatedDelta.y, accumulatedDelta.x) * Mathf.Rad2Deg;
                // 0 is right, 90 is up, 180/-180 is left, -90 is down

                // We apply this angle to the sword's local Red (X) axis.
                // This makes the sword tip (Green) rotate within the Y-Z plane 
                // to match the "heading" of your mouse pullback.
                swordAnchor.localRotation = Quaternion.Slerp(
                    swordAnchor.localRotation,
                    Quaternion.Euler(0, 0, mouseAngle-90),
                    Time.deltaTime * 20f
                );
            }

            // 3. SWING DETECTION
            float reversalDot = Vector2.Dot(mouseDelta.normalized, accumulatedDelta.normalized);

            if (accumulatedDelta.magnitude > windUpThreshold && reversalDot < -0.7f)
            {
                ExecuteSwing(mouseDelta);
            }
        }
    }

    void ExecuteSwing(Vector2 flickDelta)
    {
        isSwinging = true;

        float force = Mathf.Clamp(flickDelta.magnitude * swingSensitivity, 120f, maxSwingForce);

        // --- WORLD SPACE TORQUE ---
        // Pushes the sword opposite to the pullback direction using Camera coordinates
        Vector3 torqueDirection = (cam.transform.up * flickDelta.x) + (cam.transform.right * -flickDelta.y);

        swordRb.AddTorque(torqueDirection * force, ForceMode.Impulse);

        // Clear state
        accumulatedDelta = Vector2.zero;
        Invoke("EndSwingState", 0.4f);
    }

    void EndSwingState() => isSwinging = false;

    void ApplySwordBrake()
    {
        swordRb.angularVelocity = Vector3.zero;
        swordRb.linearVelocity = Vector3.zero;
        swordRb.angularDamping = 50f;

        accumulatedDelta = Vector2.zero;
        isSwinging = false;

        Invoke("RestorePhysics", 0.15f);
    }

    void RestorePhysics() => swordRb.angularDamping = 2f;

    void ResetAnchorSmoothly()
    {
        swordAnchor.localRotation = Quaternion.Slerp(swordAnchor.localRotation, Quaternion.identity, Time.deltaTime * returnSpeed);
    }

    // --- STANDARD MOVEMENT & LOOK ---

    void MoveInput(Vector2 moveInput)
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * moveSpeed * Time.deltaTime);
        if (isGrounded && _PlayerVelocity.y < 0) _PlayerVelocity.y = -2f;
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
        if (isGrounded) _PlayerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();
}