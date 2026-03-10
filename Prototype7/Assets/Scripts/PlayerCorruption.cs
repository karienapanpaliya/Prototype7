using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerCorruption : MonoBehaviour
{
    [Header("Corruption Timers")]
    public float safeTimeAfterSwitch = 2.5f;
    public float maxBlackTime = 7.5f;
    public float maxWhiteTime = 7.5f;

    [Header("Visual Feedback")]
    public SpriteRenderer playerRenderer;
    public Color baseColor = new Color(0.5f, 0.5f, 0.5f);
    public float minFlickerSpeed = 2f;
    public float maxFlickerSpeed = 14f;
    public float maxScalePulse = 0.2f;
    public float minPulseSpeed = 2f;
    public float maxPulseSpeed = 11f;

    [Header("Death")]
    public bool destroyOnDeath = true;
    public float destroyDelay = 0.15f;

    private enum ActiveZone
    {
        None,
        Black,
        White
    }

    private int blackContacts;
    private int whiteContacts;
    private ActiveZone activeZone;
    private float zoneTimer;
    private bool isDead;

    private PlayerMove playerMove;
    private Vector3 baseScale;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        baseScale = transform.localScale;

        if (playerRenderer == null)
        {
            playerRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (playerRenderer != null)
        {
            playerRenderer.color = baseColor;
        }

        activeZone = ActiveZone.None;
        zoneTimer = 0f;
    }

    void Update()
    {
        if (isDead)
        {
            return;
        }

        ActiveZone nextZone = ResolveActiveZone();
        if (nextZone != activeZone)
        {
            // Only reset danger when player truly swaps to the opposite zone.
            if ((activeZone == ActiveZone.Black && nextZone == ActiveZone.White) ||
                (activeZone == ActiveZone.White && nextZone == ActiveZone.Black))
            {
                zoneTimer = 0f;
            }

            activeZone = nextZone;
        }

        if (activeZone == ActiveZone.None)
        {
            ResetVisuals();
            return;
        }

        zoneTimer += Time.deltaTime;

        float zoneLimit = activeZone == ActiveZone.Black ? maxBlackTime : maxWhiteTime;
        float danger = 0f;
        if (zoneLimit > safeTimeAfterSwitch)
        {
            danger = Mathf.Clamp01((zoneTimer - safeTimeAfterSwitch) / (zoneLimit - safeTimeAfterSwitch));
        }

        UpdateVisualFeedback(danger, activeZone);

        if (zoneTimer >= zoneLimit)
        {
            Die();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CorruptionZone zone = other.GetComponent<CorruptionZone>();
        if (zone == null)
        {
            return;
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

    void OnTriggerExit2D(Collider2D other)
    {
        CorruptionZone zone = other.GetComponent<CorruptionZone>();
        if (zone == null)
        {
            return;
        }

        if (zone.zoneType == CorruptionZone.ZoneType.Black)
        {
            blackContacts = Mathf.Max(0, blackContacts - 1);
        }
        else
        {
            whiteContacts = Mathf.Max(0, whiteContacts - 1);
        }
    }

    private ActiveZone ResolveActiveZone()
    {
        if (blackContacts > 0 && whiteContacts == 0)
        {
            return ActiveZone.Black;
        }

        if (whiteContacts > 0 && blackContacts == 0)
        {
            return ActiveZone.White;
        }

        return ActiveZone.None;
    }

    private void ResetVisuals()
    {
        if (playerRenderer != null)
        {
            playerRenderer.color = baseColor;
        }

        transform.localScale = baseScale;
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

            float flickerSpeed = Mathf.Lerp(minFlickerSpeed, maxFlickerSpeed, danger);
            float pulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, danger);

            float flicker = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;
            float blend = flicker * danger;
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

        isDead = true;
        if (playerMove != null)
        {
            playerMove.SetDead();
        }

        if (playerRenderer != null)
        {
            playerRenderer.color = Color.black;
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}
