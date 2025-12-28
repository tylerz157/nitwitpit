using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerActives : MonoBehaviour
{
    PlayerInput playerInput;
    PlayerInput.MainActions input;
    CharacterController controller;

    [Header("Movement")]
    public float walkSpeed = 2f;
    public float sprintSpeed = 4f;
    public float gravity = -15f;
    public float jumpHeight = 0.6f;
    public float groundDrag = 10f;
    public float airDrag = 0.5f;

    [Header("Dash")]
    public float dashSpeed = 1f;            // Reduced from 15
    public float dashDuration = 0.2f;      // Shorter dash
    public float dashCooldown = 2f;

    [Header("Slide")]
    public float slideStartSpeed = 3f;      // Speed when slide starts
    public float slideMinSpeed = 0.3f;
    public float slideDrag = 10f;            // Much faster decay (was 2)
    public float slideCameraHeight = -0.5f;
    public float cameraLerpSpeed = 10f;

    [Header("Camera")]
    public Camera cam;
    public float sensitivity = 40f;
    private Vector3 originalCamLocalPos;

    [Header("Stamina")]
    public float maxStamina = 50f;
    public float staminaRegenRate = 5f;
    public float staminaRegenDelay = 10f;
    public float sprintStaminaCost = 15f;
    public float jumpStaminaCost = 15f;
    public float dashStaminaCost = 25f;

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Gold")]
    public int startingGold = 0;

    [Header("Emote")]
    public AudioClip emoteSound;

    // Public accessors for UI
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public int Gold => gold;
    public bool IsDashing => isDashing;
    public bool IsSliding => isSliding;
    public bool IsSprinting => isSprinting;
    public bool IsGrounded => isGrounded;
    public bool CanDash => canDash;
    public float DashCooldownPercent => canDash ? 1f : 1f - (dashCooldownTimer / dashCooldown);
    public float CurrentSpeed => _velocity.magnitude;

    // Core State
    private Vector3 _velocity;
    private float _verticalVelocity;
    private bool isGrounded;
    private float xRotation = 0f;

    // Stats
    private float currentStamina;
    private float currentHealth;
    private int gold;
    private float lastStaminaUseTime;

    // Movement State
    private bool isSprinting = false;
    private bool isSliding = false;
    private bool isDashing = false;
    private bool canDash = true;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private Vector3 dashDirection;
    private Vector3 slideDirection;

    // Audio
    private AudioSource audioSource;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        playerInput = new PlayerInput();
        input = playerInput.Main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize stats
        currentStamina = maxStamina;
        currentHealth = maxHealth;
        gold = startingGold;

        // Store original camera position
        if (cam != null)
        {
            originalCamLocalPos = cam.transform.localPosition;
        }

        // Input bindings
        input.Jump.performed += ctx => TryJump();
        input.Dash.performed += ctx => TryDash();
        input.Emote.performed += ctx => PlayEmote();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;

        // Sprint check
        isSprinting = input.Sprint.IsPressed() && !isSliding && !isDashing && currentStamina > 0;

        HandleSlideInput();
        UpdateTimers();
        UpdateStamina();
        UpdateCameraHeight();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void LateUpdate()
    {
        Vector2 lookInput = input.Look.ReadValue<Vector2>();
        LookInput(lookInput);
    }

    // ==================
    // PUBLIC METHODS (for external systems)
    // ==================

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void AddGold(int amount)
    {
        gold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (gold >= amount)
        {
            gold -= amount;
            return true;
        }
        return false;
    }

    void Die()
    {
        // Handle death - you can expand this
        Debug.Log("Player died!");
    }

    // ==================
    // SLIDE
    // ==================

    void HandleSlideInput()
    {
        bool slideHeld = input.Slide.IsPressed();

        if (slideHeld && !isSliding && !isDashing && isGrounded && _velocity.magnitude > slideMinSpeed)
        {
            StartSlide();
        }
        else if (isSliding)
        {
            bool tooSlow = _velocity.magnitude < slideMinSpeed;
            bool released = !slideHeld;
            bool leftGround = !isGrounded;

            if (tooSlow || released || leftGround)
            {
                StopSlide();
            }
        }
    }

    void StartSlide()
    {
        isSliding = true;
        slideDirection = _velocity.normalized;
        _velocity = slideDirection * Mathf.Max(_velocity.magnitude, slideStartSpeed);
    }

    void StopSlide()
    {
        isSliding = false;
    }

    // ==================
    // TIMERS
    // ==================

    void UpdateTimers()
    {
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }

        if (!canDash)
        {
            dashCooldownTimer -= Time.deltaTime;
            if (dashCooldownTimer <= 0)
            {
                canDash = true;
            }
        }
    }

    // ==================
    // CAMERA
    // ==================

    void UpdateCameraHeight()
    {
        if (cam == null) return;

        Vector3 targetPos = originalCamLocalPos;
        if (isSliding)
        {
            targetPos = originalCamLocalPos + Vector3.up * slideCameraHeight;
        }

        cam.transform.localPosition = Vector3.Lerp(
            cam.transform.localPosition,
            targetPos,
            Time.deltaTime * cameraLerpSpeed
        );
    }

    // ==================
    // STAMINA
    // ==================

    void UpdateStamina()
    {
        if (isSprinting && _velocity.magnitude > 0.1f)
        {
            UseStamina(sprintStaminaCost * Time.deltaTime);
        }

        if (Time.time > lastStaminaUseTime + staminaRegenDelay)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        }
    }

    void UseStamina(float amount)
    {
        currentStamina = Mathf.Max(0, currentStamina - amount);
        lastStaminaUseTime = Time.time;
    }

    bool HasStamina(float amount)
    {
        return currentStamina >= amount;
    }

    // ==================
    // MOVEMENT (No acceleration - instant speed changes)
    // ==================

    void HandleMovement()
    {
        Vector2 moveInput = input.Movement.ReadValue<Vector2>();
        Vector3 inputDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;

        if (isDashing)
        {
            // Dash - fixed speed in dash direction
            _velocity = dashDirection * dashSpeed;
        }
        else if (isSliding)
        {
            // Slide - decay speed quickly
            float currentSpeed = _velocity.magnitude;
            currentSpeed = Mathf.Max(0, currentSpeed - slideDrag * Time.fixedDeltaTime);
            _velocity = slideDirection * currentSpeed;
        }
        else if (isGrounded)
        {
            // Ground movement - instant speed (no acceleration)
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            
            if (moveInput.magnitude > 0.1f)
            {
                _velocity = inputDirection * targetSpeed;
            }
            else
            {
                // Quick stop when no input
                _velocity = Vector3.Lerp(_velocity, Vector3.zero, groundDrag * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Air - reduced control, keep momentum
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            Vector3 airInfluence = inputDirection * targetSpeed * 0.2f;
            _velocity += airInfluence * Time.fixedDeltaTime;
            _velocity = Vector3.Lerp(_velocity, Vector3.zero, airDrag * Time.fixedDeltaTime);
        }

        // Gravity
        if (isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -3f;
        }
        else
        {
            _verticalVelocity += gravity * Time.fixedDeltaTime;
        }

        // Apply
        Vector3 finalMove = _velocity + Vector3.up * _verticalVelocity;
        controller.Move(finalMove * Time.fixedDeltaTime);
    }

    // ==================
    // JUMP
    // ==================

    void TryJump()
    {
        if (!isGrounded) return;
        if (isSliding) StopSlide();
        if (!HasStamina(jumpStaminaCost)) return;

        UseStamina(jumpStaminaCost);
        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // ==================
    // DASH
    // ==================

    void TryDash()
    {
        if (isDashing) return;
        if (!canDash) return;
        if (!HasStamina(dashStaminaCost)) return;

        if (isSliding) StopSlide();

        UseStamina(dashStaminaCost);

        isDashing = true;
        canDash = false;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        Vector2 moveInput = input.Movement.ReadValue<Vector2>();
        if (moveInput.magnitude > 0.1f)
        {
            dashDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        }
        else
        {
            dashDirection = transform.forward;
        }

        _verticalVelocity *= 0.2f;
    }

    // ==================
    // EMOTE
    // ==================

    void PlayEmote()
    {
        if (emoteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(emoteSound);
        }
    }

    // ==================
    // CAMERA LOOK
    // ==================

    void LookInput(Vector2 lookInput)
    {
        float mouseX = lookInput.x * sensitivity * Time.deltaTime;
        float mouseY = lookInput.y * sensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        transform.Rotate(Vector3.up * mouseX);
    }

    // ==================
    // INPUT
    // ==================

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();
}