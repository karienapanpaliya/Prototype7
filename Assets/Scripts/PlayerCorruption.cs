using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerMove))]
public class PlayerCorruption : MonoBehaviour
{
    [Header("Corruption Timers")]
    public float safeDuration = 3f;
    public float deathDuration = 8f;

    [Header("Visual Feedback")]
    public SpriteRenderer playerRenderer;
    public float minFlickerSpeed = 0.8f;
    public float maxFlickerSpeed = 18f;
    public float maxScalePulse = 0.2f;
    public float minPulseSpeed = 2f;
    public float maxPulseSpeed = 11f;

    [Header("Early Warning")]
    public bool autoCreateFeedbackHUD = true;
    public ParticleSystem leakParticles;
    public SpriteRenderer auraRenderer;
    public float minLeakRate = 2f;
    public float maxLeakRate = 16f;
    public float minLeakSpeed = 0.12f;
    public float maxLeakSpeed = 0.8f;
    public float minLeakSize = 0.05f;
    public float maxLeakSize = 0.16f;
    public float leakLifetime = 0.55f;
    public float minAuraScale = 1.2f;
    public float maxAuraScale = 2.4f;
    public float maxAuraAlpha = 0.3f;

    [Header("Audio")]
    public AudioSource heartbeatSource;
    public AudioClip heartbeatClip;
    public float minHeartbeatInterval = 0.9f;
    public float maxHeartbeatInterval = 0.22f;
    public float maxHeartbeatVolume = 0.6f;
    public float minHeartbeatPitch = 0.94f;
    public float maxHeartbeatPitch = 1.08f;

    [Header("Death")]
    public bool destroyOnDeath = true;
    public float destroyDelay = 0.15f;
    public bool resetSceneOnDeath = true;
    public float resetDelay = 0.3f;

    [Header("Controls")]
    public bool allowManualRestart = true;
    public KeyCode restartKey = KeyCode.R;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    protected enum ActiveZone
    {
        None,
        Black,
        White
    }

    private int blackContacts;
    private int whiteContacts;
    private readonly List<CorruptionZone> overlappingZones = new List<CorruptionZone>();
    protected ActiveZone activeZone;
    protected float zoneTimer;
    private bool isDead;
    private bool isSafeMode;
    protected float currentExposureProgress;
    protected float currentDangerProgress;
    protected float currentSafeWindowProgress;
    protected bool hasBecomeUnsafe;
    protected ActiveZone lastCommittedZone;
    protected float lastCleanSwapTime = -999f;
    protected Vector2 lastCleanSwapPosition;

    private PlayerMove playerMove;
    private Color baseColor;
    private Vector3 baseScale;
    private Vector3 auraBaseScale;
    private float heartbeatTimer;

    public bool IsInCorruptingZone => activeZone != ActiveZone.None;
    public float ExposureProgress => currentExposureProgress;
    public float DangerProgress => currentDangerProgress;
    public float SafeWindowProgress => currentSafeWindowProgress;
    public bool IsBlackZoneActive => activeZone == ActiveZone.Black;
    public Color ActiveZoneColor => activeZone == ActiveZone.Black ? Color.black : Color.white;
    public float ZoneTimer => zoneTimer;
    public float SafeDuration => safeDuration;
    public float DeathDuration => deathDuration;
    // Gray/neutral space is safe, and corruption zones are safe until danger starts.
    public bool IsStable => !IsInCorruptingZone || currentDangerProgress <= 0.0001f;
    public float LastCleanSwapTime => lastCleanSwapTime;
    public Vector2 LastCleanSwapPosition => lastCleanSwapPosition;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        baseScale = transform.localScale;

        if (playerRenderer == null)
        {
            playerRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Always read from the renderer so the color set in the editor/prefab is never overwritten.
        baseColor = playerRenderer != null ? playerRenderer.color : Color.white;

        activeZone = ActiveZone.None;
        zoneTimer = 0f;
        currentExposureProgress = 0f;
        currentDangerProgress = 0f;
        currentSafeWindowProgress = 0f;
        hasBecomeUnsafe = false;
        lastCommittedZone = ActiveZone.None;

        if (autoCreateFeedbackHUD)
        {
            CorruptionFeedbackHUD.EnsureFor(this);
        }

        if (leakParticles == null)
        {
            leakParticles = CreateLeakParticles();
        }

        if (auraRenderer == null)
        {
            auraRenderer = CreateAuraRenderer();
        }

        if (auraRenderer != null)
        {
            auraBaseScale = auraRenderer.transform.localScale;
        }

        EnsureHeartbeatSource();

        UpdateLeakParticles(0f, activeZone);
        UpdateAura(0f, activeZone);
    }

    protected virtual void Update()
    {
        if (allowManualRestart && IsRestartPressed())
        {
            RestartSceneNow();
            return;
        }

        if (isDead)
        {
            StopHeartbeat();
            return;
        }

        if (isSafeMode)
        {
            StopHeartbeat();
            return;
        }

        ActiveZone nextZone = ResolveActiveZone();
        if (nextZone != activeZone)
        {
            ActiveZone previousZone = activeZone;

            // Reset when we commit to the opposite side, even if the boundary briefly resolves to None.
            if (nextZone != ActiveZone.None &&
                lastCommittedZone != ActiveZone.None &&
                nextZone != lastCommittedZone)
            {
                DebugLog("Switched sides from " + lastCommittedZone + " to " + nextZone + ". Resetting timer from " + zoneTimer.ToString("F2") + " to 0.");
                zoneTimer = 0f;
                hasBecomeUnsafe = false;
                lastCleanSwapTime = Time.time;
                lastCleanSwapPosition = transform.position;
            }

            DebugLog("Active zone changed from " + previousZone + " to " + nextZone +
                ". Contacts -> black: " + blackContacts + ", white: " + whiteContacts + ", timer: " + zoneTimer.ToString("F2"));

            activeZone = nextZone;

            if (activeZone != ActiveZone.None)
            {
                lastCommittedZone = activeZone;
            }
        }

        if (activeZone == ActiveZone.None)
        {
            // Neutral gray space is a full safe zone: recover timer and danger state.
            if (zoneTimer > 0f)
            {
                DebugLog("Entered gray safe zone. Resetting timer from " + zoneTimer.ToString("F2") + " to 0.");
            }

            zoneTimer = 0f;
            currentExposureProgress = 0f;
            currentDangerProgress = 0f;
            currentSafeWindowProgress = 0f;
            hasBecomeUnsafe = false;
            ResetVisuals();
            StopHeartbeat();
            return;
        }

        zoneTimer += Time.deltaTime;

        float safeTime = Mathf.Max(0.01f, safeDuration);
        float zoneLimit = Mathf.Max(safeTime + 0.01f, deathDuration);
        currentExposureProgress = Mathf.Clamp01(zoneTimer / zoneLimit);
        currentSafeWindowProgress = Mathf.Clamp01(zoneTimer / safeTime);
        float danger = 0f;
        if (zoneLimit > safeTime)
        {
            danger = Mathf.Clamp01((zoneTimer - safeTime) / (zoneLimit - safeTime));
        }

        currentDangerProgress = danger;
        float anticipation = Mathf.Max(currentExposureProgress * 0.65f, danger);

        if (!hasBecomeUnsafe && zoneTimer >= safeTime)
        {
            hasBecomeUnsafe = true;
            DebugLog("Player became unsafe in " + activeZone + " zone at timer " + zoneTimer.ToString("F2") +
                ". Death at " + zoneLimit.ToString("F2") + ".");
        }

        UpdateVisualFeedback(danger, activeZone);
        UpdateLeakParticles(anticipation, activeZone);
        UpdateAura(anticipation, activeZone);
        UpdateHeartbeat(danger);

        if (zoneTimer >= zoneLimit)
        {
            Die();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CorruptionZone zone = ResolveZoneFromCollider(other);
        if (zone == null)
        {
            return;
        }

        if (!overlappingZones.Contains(zone))
        {
            overlappingZones.Add(zone);
            RecountZoneContacts();
        }

        DebugLog("Trigger enter: " + zone.zoneType + " on " + zone.name + ". Contacts -> black: " + blackContacts + ", white: " + whiteContacts +
            ", resolved zone: " + ResolveActiveZone() + ", timer: " + zoneTimer.ToString("F2"));
    }

    void OnTriggerStay2D(Collider2D other)
    {
        CorruptionZone zone = ResolveZoneFromCollider(other);
        if (zone == null)
        {
            return;
        }

        if (!overlappingZones.Contains(zone))
        {
            overlappingZones.Add(zone);
            RecountZoneContacts();
            DebugLog("Trigger stay sync: added missing overlap for " + zone.zoneType + " on " + zone.name + ".");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        CorruptionZone zone = ResolveZoneFromCollider(other);
        if (zone == null)
        {
            return;
        }

        if (overlappingZones.Remove(zone))
        {
            RecountZoneContacts();
        }

        DebugLog("Trigger exit: " + zone.zoneType + " on " + zone.name + ". Contacts -> black: " + blackContacts + ", white: " + whiteContacts +
            ", resolved zone: " + ResolveActiveZone() + ", timer: " + zoneTimer.ToString("F2"));
    }

    private CorruptionZone ResolveZoneFromCollider(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        CorruptionZone zone = other.GetComponent<CorruptionZone>();
        if (zone != null)
        {
            return zone;
        }

        zone = other.GetComponentInParent<CorruptionZone>();
        if (zone != null)
        {
            return zone;
        }

        return other.GetComponentInChildren<CorruptionZone>();
    }

    private void RecountZoneContacts()
    {
        blackContacts = 0;
        whiteContacts = 0;

        for (int index = overlappingZones.Count - 1; index >= 0; index--)
        {
            CorruptionZone zone = overlappingZones[index];
            if (zone == null)
            {
                overlappingZones.RemoveAt(index);
                continue;
            }

            if (zone.zoneType == CorruptionZone.ZoneType.Black)
            {
                blackContacts++;
            }
            else
            {
                whiteContacts++;
            }
        }
    }

    protected virtual ActiveZone ResolveActiveZone()
    {
        if (overlappingZones.Count == 0)
        {
            return ActiveZone.None;
        }

        CorruptionZone bestZone = null;
        for (int index = overlappingZones.Count - 1; index >= 0; index--)
        {
            CorruptionZone zone = overlappingZones[index];
            if (zone == null)
            {
                overlappingZones.RemoveAt(index);
                continue;
            }

            if (bestZone == null || zone.zonePriority > bestZone.zonePriority)
            {
                bestZone = zone;
            }
        }

        if (bestZone == null)
        {
            return ActiveZone.None;
        }

        return bestZone.zoneType == CorruptionZone.ZoneType.Black
            ? ActiveZone.Black
            : ActiveZone.White;
    }

    private void ResetVisuals()
    {
        if (playerRenderer != null)
        {
            playerRenderer.color = baseColor;
        }

        transform.localScale = baseScale;
        UpdateLeakParticles(0f, activeZone);
        UpdateAura(0f, activeZone);
    }

    private void UpdateVisualFeedback(float danger, ActiveZone zone)
    {
        if (danger <= 0f)
        {
            ResetVisuals();
            return;
        }

        if (playerRenderer != null)
        {
            Color targetColor = zone == ActiveZone.Black ? Color.black : Color.white;

            // Use a non-linear ramp so flicker begins slow, then accelerates near high danger.
            float speedRamp = Mathf.Pow(Mathf.Clamp01(danger), 1.8f);
            float flickerSpeed = Mathf.Lerp(minFlickerSpeed, maxFlickerSpeed, speedRamp);
            float pulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, speedRamp);

            float flicker = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;
            // Ensure flicker is visible as soon as danger starts, then ramp to full intensity.
            float blend = flicker * Mathf.Lerp(0.12f, 1f, speedRamp);
            playerRenderer.color = Color.Lerp(baseColor, targetColor, blend);

            float pulse = Mathf.Sin(Time.time * pulseSpeed) * maxScalePulse * danger;
            transform.localScale = baseScale * (1f + pulse);
            return;
        }

        // Keep pulsing even if renderer reference is missing.
        float fallbackPulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, danger);
        float fallbackPulse = Mathf.Sin(Time.time * fallbackPulseSpeed) * maxScalePulse * danger;
        transform.localScale = baseScale * (1f + fallbackPulse);
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        DebugLog("Player died in zone " + activeZone + " at timer " + zoneTimer.ToString("F2") +
            ". Contacts -> black: " + blackContacts + ", white: " + whiteContacts);

        isDead = true;
        if (playerMove != null)
        {
            playerMove.SetDead();
        }

        if (playerRenderer != null)
        {
            playerRenderer.color = Color.black;
        }

        UpdateLeakParticles(1f, activeZone == ActiveZone.None ? ActiveZone.Black : activeZone);
        UpdateAura(1f, activeZone == ActiveZone.None ? ActiveZone.Black : activeZone);
        StopHeartbeat();

        if (resetSceneOnDeath)
        {
            StartCoroutine(ResetSceneAfterDelay());
            return;
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private IEnumerator ResetSceneAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        RestartSceneNow();
    }

    private void RestartSceneNow()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private bool IsRestartPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return restartKey switch
        {
            KeyCode.R => keyboard.rKey.wasPressedThisFrame,
            KeyCode.Space => keyboard.spaceKey.wasPressedThisFrame,
            KeyCode.Escape => keyboard.escapeKey.wasPressedThisFrame,
            KeyCode.Return => keyboard.enterKey.wasPressedThisFrame,
            KeyCode.KeypadEnter => keyboard.numpadEnterKey.wasPressedThisFrame,
            _ => false
        };
    }

    public void ResetCorruption()
    {
        DebugLog("Reset corruption on safe zone.");
        zoneTimer = 0f;
        currentExposureProgress = 0f;
        currentDangerProgress = 0f;
        currentSafeWindowProgress = 0f;
        hasBecomeUnsafe = false;
        StopHeartbeat();
        ResetVisuals();
    }

    public void SetSafeMode(bool state)
    {
        isSafeMode = state;
        if (isSafeMode)
        {
            ResetCorruption();
        }
    }

    private void DebugLog(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log("[PlayerCorruption] " + message, this);
    }

    private ParticleSystem CreateLeakParticles()
    {
        GameObject particleObject = new GameObject("CorruptionLeakParticles");
        particleObject.transform.SetParent(transform, false);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = leakLifetime;
        main.startSpeed = minLeakSpeed;
        main.startSize = minLeakSize;
        main.maxParticles = 64;
        main.gravityModifier = 0f;

        var emission = particles.emission;
        emission.rateOverTime = 0f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.08f;

        var velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.35f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

        var rendererModule = particles.GetComponent<ParticleSystemRenderer>();
        rendererModule.renderMode = ParticleSystemRenderMode.Billboard;
        rendererModule.sortingOrder = 10;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private void EnsureHeartbeatSource()
    {
        if (heartbeatSource != null)
        {
            heartbeatSource.playOnAwake = false;
            heartbeatSource.loop = false;
            heartbeatSource.spatialBlend = 0f;
            return;
        }

        GameObject audioObject = new GameObject("HeartbeatAudio");
        audioObject.transform.SetParent(transform, false);
        heartbeatSource = audioObject.AddComponent<AudioSource>();
        heartbeatSource.playOnAwake = false;
        heartbeatSource.loop = false;
        heartbeatSource.spatialBlend = 0f;
    }

    private void UpdateHeartbeat(float danger)
    {
        if (heartbeatSource == null || heartbeatClip == null)
        {
            return;
        }

        if (danger <= 0f)
        {
            StopHeartbeat();
            return;
        }

        float speedRamp = Mathf.Pow(Mathf.Clamp01(danger), 1.8f);
        float interval = Mathf.Lerp(minHeartbeatInterval, maxHeartbeatInterval, speedRamp);

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer > 0f)
        {
            return;
        }

        heartbeatSource.pitch = Mathf.Lerp(minHeartbeatPitch, maxHeartbeatPitch, speedRamp);
        float volume = Mathf.Lerp(0.2f, maxHeartbeatVolume, danger);
        heartbeatSource.PlayOneShot(heartbeatClip, volume);
        heartbeatTimer = interval;
    }

    private void StopHeartbeat()
    {
        heartbeatTimer = 0f;
        if (heartbeatSource != null && heartbeatSource.isPlaying)
        {
            heartbeatSource.Stop();
        }
    }

    private SpriteRenderer CreateAuraRenderer()
    {
        GameObject auraObject = new GameObject("CorruptionAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localScale = Vector3.one;

        SpriteRenderer aura = auraObject.AddComponent<SpriteRenderer>();
        aura.sprite = CorruptionFeedbackHUD.GetOrCreateAuraSprite();
        aura.sortingOrder = -1;
        aura.color = new Color(1f, 1f, 1f, 0f);
        return aura;
    }

    private void UpdateLeakParticles(float intensity, ActiveZone zone)
    {
        if (leakParticles == null)
        {
            return;
        }

        intensity = Mathf.Clamp01(intensity);

        var main = leakParticles.main;
        main.startLifetime = Mathf.Lerp(leakLifetime * 0.7f, leakLifetime * 1.25f, intensity);
        main.startSpeed = Mathf.Lerp(minLeakSpeed, maxLeakSpeed, intensity);
        main.startSize = Mathf.Lerp(minLeakSize, maxLeakSize, intensity);

        Color particleColor = zone == ActiveZone.White
            ? new Color(0.9f, 0.97f, 1f, 0.75f)
            : new Color(0.08f, 0.08f, 0.1f, 0.85f);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(particleColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(Mathf.Lerp(0.1f, 0.5f, intensity), 0.2f),
                new GradientAlphaKey(Mathf.Lerp(0.08f, 0.3f, intensity), 0.8f),
                new GradientAlphaKey(0f, 1f)
            });

        var colorOverLifetime = leakParticles.colorOverLifetime;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var emission = leakParticles.emission;
        emission.rateOverTime = Mathf.Lerp(0f, maxLeakRate, intensity);

        if (intensity > 0.01f)
        {
            if (!leakParticles.isPlaying)
            {
                leakParticles.Play();
            }
        }
        else if (leakParticles.isPlaying)
        {
            leakParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateAura(float intensity, ActiveZone zone)
    {
        if (auraRenderer == null)
        {
            return;
        }

        intensity = Mathf.Clamp01(intensity);
        if (auraBaseScale == Vector3.zero)
        {
            auraBaseScale = Vector3.one;
        }

        float pulse = 1f + (Mathf.Sin(Time.time * Mathf.Lerp(1.4f, 5.5f, intensity)) * 0.06f * intensity);
        float scale = Mathf.Lerp(minAuraScale, maxAuraScale, intensity) * pulse;
        auraRenderer.transform.localScale = auraBaseScale * scale;

        Color zoneColor = zone == ActiveZone.White
            ? new Color(0.82f, 0.93f, 1f, 1f)
            : new Color(0.08f, 0.08f, 0.12f, 1f);
        auraRenderer.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, intensity * maxAuraAlpha);
        auraRenderer.enabled = intensity > 0.01f;
    }
}
