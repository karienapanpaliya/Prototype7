using UnityEngine;

public class CorruptionFeedbackHUD : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerCorruption target;

    [Header("Vignette")]
    [SerializeField] private int vignetteLayers = 11;
    [SerializeField] private float minVignetteDepth = 36f;
    [SerializeField] private float maxVignetteDepth = 185f;
    [SerializeField] private float minVignetteAlpha = 0.06f;
    [SerializeField] private float maxVignetteAlpha = 0.34f;
    [SerializeField] private float grainAmount = 0.2f;
    [SerializeField] private float grainSpeed = 14f;
    [SerializeField] private bool blackZoneOnLeftSide = true;

    private static Texture2D whiteTexture;
    private static Sprite auraSprite;

    public static CorruptionFeedbackHUD EnsureFor(PlayerCorruption corruption)
    {
        if (corruption == null)
        {
            return null;
        }

        CorruptionFeedbackHUD hud = FindFirstObjectByType<CorruptionFeedbackHUD>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("CorruptionFeedbackHUD");
            hud = hudObject.AddComponent<CorruptionFeedbackHUD>();
        }

        hud.target = corruption;
        return hud;
    }

    void Awake()
    {
        EnsureTexture();

        if (target == null)
        {
            target = FindFirstObjectByType<PlayerCorruption>();
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (target == null)
        {
            target = FindFirstObjectByType<PlayerCorruption>();
        }

        if (target == null)
        {
            return;
        }

        float danger = target.DangerProgress;
        if (danger <= 0f)
        {
            return;
        }

        Color zoneColor = GetZoneColor(target.IsBlackZoneActive);
        Rect sideRect = GetActiveSideRect(target.IsBlackZoneActive);

        DrawVignette(zoneColor, danger, sideRect);
    }

    public static Sprite GetOrCreateAuraSprite()
    {
        if (auraSprite != null)
        {
            return auraSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = center.magnitude;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        auraSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return auraSprite;
    }

    private void DrawVignette(Color zoneColor, float intensity, Rect area)
    {
        if (intensity <= 0f)
        {
            return;
        }

        intensity = Mathf.Clamp01(intensity);
        int layers = Mathf.Max(1, vignetteLayers);
        float depth = Mathf.Lerp(minVignetteDepth, maxVignetteDepth, intensity);
        float pulse = 0.92f + ((Mathf.Sin(Time.time * Mathf.Lerp(1.5f, 6f, intensity)) + 1f) * 0.04f);
        float totalAlpha = Mathf.Lerp(minVignetteAlpha, maxVignetteAlpha, intensity) * pulse;

        for (int index = 0; index < layers; index++)
        {
            float t = (index + 1f) / layers;
            float layerInset = depth * (1f - Mathf.Pow(t, 1.75f));
            float layerAlpha = totalAlpha * (1f - t) * 0.8f;

            // Add subtle moving noise so the vignette feels grainy and unstable.
            float grainSample = Mathf.PerlinNoise((index * 0.37f) + (Time.time * grainSpeed), Time.time * (grainSpeed * 0.71f));
            float grainMultiplier = Mathf.Lerp(1f - grainAmount, 1f + grainAmount, grainSample);
            layerAlpha *= grainMultiplier;

            if (layerAlpha <= 0f)
            {
                continue;
            }

            Color layerColor = new Color(zoneColor.r, zoneColor.g, zoneColor.b, layerAlpha);
            DrawBorder(layerInset, layerColor, area);
        }
    }

    private Rect GetActiveSideRect(bool isBlackZone)
    {
        float halfWidth = Screen.width * 0.5f;
        bool drawLeftSide = isBlackZone ? blackZoneOnLeftSide : !blackZoneOnLeftSide;
        return drawLeftSide
            ? new Rect(0f, 0f, halfWidth, Screen.height)
            : new Rect(halfWidth, 0f, halfWidth, Screen.height);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = previousColor;
    }

    private static Color GetZoneColor(bool isBlackZone)
    {
        // Invert vignette color for stronger contrast with the active zone.
        return isBlackZone
            ? new Color(0.96f, 0.96f, 0.96f, 1f)
            : new Color(0.04f, 0.04f, 0.05f, 1f);
    }

    private static void DrawBorder(float inset, Color color, Rect area)
    {
        float width = area.width;
        float height = area.height;
        float left = Mathf.Clamp(inset, 0f, width * 0.5f);
        float top = Mathf.Clamp(inset, 0f, height * 0.5f);
        float innerWidth = Mathf.Max(0f, width - (left * 2f));
        float innerHeight = Mathf.Max(0f, height - (top * 2f));
        float x = area.x;
        float y = area.y;

        DrawRect(new Rect(x, y, width, top), color);
        DrawRect(new Rect(x, y + height - top, width, top), color);
        DrawRect(new Rect(x, y + top, left, innerHeight), color);
        DrawRect(new Rect(x + width - left, y + top, left, innerHeight), color);
    }

    private static void EnsureTexture()
    {
        if (whiteTexture != null)
        {
            return;
        }

        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }
}