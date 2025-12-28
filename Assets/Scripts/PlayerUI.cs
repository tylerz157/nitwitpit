using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerUI : MonoBehaviour
{
    [Header("Player Reference")]
    public PlayerActives player;

    [Header("UI Elements (Optional - will use OnGUI if not assigned)")]
    public Image healthBarFill;
    public Image staminaBarFill;
    public Text goldText;

    [Header("Screen Effects")]
    public Volume postProcessVolume;
    public float maxVignetteIntensity = 0.5f;     // How dark screen gets at 0 stamina
    public float vignetteSmoothing = 5f;
    
    [Header("Motion Blur")]
    public float dashBlurIntensity = 0.4f;
    public float sprintBlurIntensity = 0.1f;
    public float blurSmoothing = 8f;

    [Header("UI Settings")]
    public bool useCanvasUI = false;  // Set true if using Unity UI, false for OnGUI
    public Color healthColor = new Color(0.8f, 0.2f, 0.2f, 1f);      // Red
    public Color healthBgColor = new Color(0.3f, 0.1f, 0.1f, 0.8f);  // Dark red
    public Color staminaColor = new Color(0.9f, 0.8f, 0.2f, 1f);     // Yellow
    public Color staminaBgColor = new Color(0.4f, 0.35f, 0.1f, 0.8f); // Dark yellow
    public Color goldColor = new Color(1f, 0.85f, 0.3f, 1f);         // Gold

    // Post processing
    private Vignette vignette;
    private MotionBlur motionBlur;
    private float targetVignette = 0f;
    private float targetBlur = 0f;

    // Textures for OnGUI
    private Texture2D whiteTexture;
    private Texture2D bgTexture;

    void Awake()
    {
        // Find player if not assigned
        if (player == null)
        {
            player = FindObjectOfType<PlayerActives>();
        }

        // Setup post processing
        SetupPostProcessing();

        // Create textures for OnGUI
        CreateTextures();
    }

    void CreateTextures()
    {
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
        bgTexture.Apply();
    }

    void SetupPostProcessing()
    {
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out motionBlur);
        }
    }

    void Update()
    {
        if (player == null) return;

        UpdateScreenEffects();
        UpdateMotionBlur();

        // Update Canvas UI if using it
        if (useCanvasUI)
        {
            UpdateCanvasUI();
        }
    }

    // ==================
    // SCREEN EFFECTS
    // ==================

    void UpdateScreenEffects()
    {
        // Screen darkens as stamina decreases
        float staminaPercent = player.CurrentStamina / player.MaxStamina;
        
        // Vignette increases as stamina decreases (inverse relationship)
        // Start darkening below 50% stamina
        if (staminaPercent < 0.5f)
        {
            float darkenAmount = 1f - (staminaPercent / 0.5f); // 0 at 50%, 1 at 0%
            targetVignette = darkenAmount * maxVignetteIntensity;
        }
        else
        {
            targetVignette = 0f;
        }

        // Apply vignette
        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(
                vignette.intensity.value,
                targetVignette,
                Time.deltaTime * vignetteSmoothing
            );
        }
    }

    void UpdateMotionBlur()
    {
        // Motion blur based on movement state
        if (player.IsDashing)
        {
            targetBlur = dashBlurIntensity;
        }
        else if (player.IsSprinting && player.CurrentSpeed > 1f)
        {
            targetBlur = sprintBlurIntensity;
        }
        else
        {
            targetBlur = 0f;
        }

        if (motionBlur != null)
        {
            motionBlur.intensity.value = Mathf.Lerp(
                motionBlur.intensity.value,
                targetBlur,
                Time.deltaTime * blurSmoothing
            );
        }
    }

    // ==================
    // CANVAS UI (if using Unity UI)
    // ==================

    void UpdateCanvasUI()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = player.CurrentHealth / player.MaxHealth;
        }

        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = player.CurrentStamina / player.MaxStamina;
        }

        if (goldText != null)
        {
            goldText.text = player.Gold.ToString();
        }
    }

    // ==================
    // OnGUI (Built-in UI)
    // ==================

    void OnGUI()
    {
        if (player == null || useCanvasUI) return;

        // UI positioning
        float screenW = Screen.width;
        float screenH = Screen.height;

        // Bar dimensions
        float barWidth = 300f;
        float barHeight = 20f;
        float barSpacing = 8f;
        float bottomPadding = 40f;
        float cornerRadius = 4f;

        // Position (bottom center)
        float barsX = (screenW - barWidth) / 2f;
        float healthY = screenH - bottomPadding - barHeight;
        float staminaY = healthY - barHeight - barSpacing;

        // Gold position (left of bars)
        float goldX = barsX - 100f;
        float goldY = healthY - barHeight / 2f;

        // Calculate fill amounts
        float healthPercent = player.CurrentHealth / player.MaxHealth;
        float staminaPercent = player.CurrentStamina / player.MaxStamina;

        // Draw Health Bar
        DrawBar(barsX, healthY, barWidth, barHeight, healthPercent, healthColor, healthBgColor, "HP");

        // Draw Stamina Bar
        DrawBar(barsX, staminaY, barWidth, barHeight, staminaPercent, staminaColor, staminaBgColor, "ST");

        // Draw Gold
        DrawGold(goldX, goldY, player.Gold);

        // Draw Dash Cooldown (small indicator above stamina)
        if (!player.CanDash)
        {
            float cdWidth = 60f;
            float cdHeight = 6f;
            float cdX = barsX + barWidth + 10f;
            float cdY = staminaY + (barHeight - cdHeight) / 2f;
            DrawBar(cdX, cdY, cdWidth, cdHeight, player.DashCooldownPercent, Color.cyan, new Color(0, 0.3f, 0.3f, 0.8f), "");
        }

        // Debug info (optional - top left)
        DrawDebugInfo();
    }

    void DrawBar(float x, float y, float width, float height, float fillPercent, Color fillColor, Color bgColor, string label)
    {
        // Background
        GUI.color = bgColor;
        GUI.DrawTexture(new Rect(x, y, width, height), whiteTexture);

        // Fill
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(x + 2, y + 2, (width - 4) * fillPercent, height - 4), whiteTexture);

        // Border
        GUI.color = new Color(0, 0, 0, 0.8f);
        // Top
        GUI.DrawTexture(new Rect(x, y, width, 2), whiteTexture);
        // Bottom
        GUI.DrawTexture(new Rect(x, y + height - 2, width, 2), whiteTexture);
        // Left
        GUI.DrawTexture(new Rect(x, y, 2, height), whiteTexture);
        // Right
        GUI.DrawTexture(new Rect(x + width - 2, y, 2, height), whiteTexture);

        // Label
        if (!string.IsNullOrEmpty(label))
        {
            GUI.color = Color.white;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            
            // Shadow
            GUI.color = Color.black;
            GUI.Label(new Rect(x + 7, y + 1, 30, height), label, labelStyle);
            
            // Text
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 6, y, 30, height), label, labelStyle);
        }

        GUI.color = Color.white;
    }

    void DrawGold(float x, float y, int amount)
    {
        GUIStyle goldStyle = new GUIStyle(GUI.skin.label);
        goldStyle.fontSize = 24;
        goldStyle.fontStyle = FontStyle.Bold;
        goldStyle.alignment = TextAnchor.MiddleRight;

        string goldStr = amount.ToString();

        // Shadow
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, 80, 30), goldStr, goldStyle);

        // Gold text
        GUI.color = goldColor;
        GUI.Label(new Rect(x, y, 80, 30), goldStr, goldStyle);

        // Gold icon/label
        GUIStyle iconStyle = new GUIStyle(goldStyle);
        iconStyle.fontSize = 16;
        iconStyle.alignment = TextAnchor.MiddleRight;
        
        GUI.color = new Color(1f, 0.7f, 0.2f, 0.8f);
        GUI.Label(new Rect(x, y - 20, 80, 20), "GOLD", iconStyle);

        GUI.color = Color.white;
    }

    void DrawDebugInfo()
    {
        // Small debug info in top-left (optional)
        GUIStyle debugStyle = new GUIStyle(GUI.skin.label);
        debugStyle.fontSize = 14;
        debugStyle.normal.textColor = Color.white;

        string state = "Idle";
        if (player.IsDashing) state = "DASH";
        else if (player.IsSliding) state = "SLIDE";
        else if (player.IsSprinting) state = "Sprint";
        else if (!player.IsGrounded) state = "Air";

        // Background box
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(new Rect(5, 5, 120, 50), whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(10, 8, 200, 20), $"State: {state}", debugStyle);
        GUI.Label(new Rect(10, 26, 200, 20), $"Speed: {player.CurrentSpeed:F1}", debugStyle);
    }

    void OnDestroy()
    {
        // Clean up textures
        if (whiteTexture != null) Destroy(whiteTexture);
        if (bgTexture != null) Destroy(bgTexture);
    }
}