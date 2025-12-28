using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

public class PlayerController : MonoBehaviour
{
    PlayerInput playerInput;
    PlayerInput.MainActions input;
    CharacterController controller;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Camera")]
    public Camera cam;
    public float sensitivity = 100f;

    [Header("Physics Sword")]
    public ConfigurableJoint swordJoint;
    public Transform swordAnchor;
    public float swordSwayIntensity = 300f;
    public float swordSmoothSpeed = 10f;

    [Header("IK Rigging")]
    public Rig armRig;

    private Vector3 _PlayerVelocity;
    private bool isGrounded;
    private float xRotation = 0f;
    private bool isAttackingMode = false;
    private Quaternion swordTargetRotation = Quaternion.identity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = new @PlayerInput(); // Use the @ symbol as defined in your generated class
        input = playerInput.Main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        input.Jump.performed += ctx => Jump();

        if (swordJoint != null)
            swordTargetRotation = swordJoint.transform.localRotation;
    }

    void Update()
    {
        isGrounded = controller.isGrounded;

        // Check the attack button state
        isAttackingMode = input.Attack.IsPressed();

        // EMERGENCY DEBUG 1: Is the button working?
        if (isAttackingMode)
        {
            Debug.Log("Left Click Detected!");
        }

        if (armRig != null)
        {
            armRig.weight = Mathf.Lerp(armRig.weight, isAttackingMode ? 1f : 0.8f, Time.deltaTime * 5f);
        }
    }

    void FixedUpdate()
    {
        MoveInput(input.Movement.ReadValue<Vector2>());
        UpdateSwordPhysics();
    }

    void LateUpdate()
    {
        Vector2 mouseDelta = input.Look.ReadValue<Vector2>();

        if (!isAttackingMode)
        {
            LookInput(mouseDelta);
        }
        else
        {
            // EMERGENCY DEBUG 2: Is the mouse moving?
            if (mouseDelta.sqrMagnitude > 0)
            {
                Debug.Log("Swinging Sword! Mouse Delta: " + mouseDelta);
            }
            SwingSword(mouseDelta);
        }
    }

    void MoveInput(Vector2 moveInput)
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * moveSpeed * Time.deltaTime);
        _PlayerVelocity.y += gravity * Time.deltaTime;
        if (isGrounded && _PlayerVelocity.y < 0) _PlayerVelocity.y = -2f;
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

    void SwingSword(Vector2 mouseDelta)
    {
        if (swordJoint == null || swordAnchor == null) return;

        float swingX = mouseDelta.x * swordSwayIntensity * Time.deltaTime;
        float swingY = mouseDelta.y * swordSwayIntensity * Time.deltaTime;

        swordAnchor.localRotation *= Quaternion.Euler(-swingY, swingX, 0);
        swordTargetRotation = swordAnchor.localRotation;
    }

    void UpdateSwordPhysics()
    {
        if (swordJoint == null) return;
        swordJoint.targetPosition = Vector3.zero;

        if (!isAttackingMode)
        {
            swordTargetRotation = Quaternion.Slerp(swordTargetRotation, Quaternion.identity, Time.deltaTime * swordSmoothSpeed);
            // Sync the anchor back so it doesn't "snap" when you click again
            swordAnchor.localRotation = swordTargetRotation;
        }

        swordJoint.targetRotation = swordTargetRotation;
    }

    void Jump()
    {
        if (isGrounded) _PlayerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();
}