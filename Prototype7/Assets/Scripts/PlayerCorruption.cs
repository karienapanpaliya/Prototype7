using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerCorruption : MonoBehaviour
{
    [Header("Corruption Timers")]
    public float maxBlackTime = 2.5f;
    public float maxWhiteTime = 2.5f;
    public float recoveryPerSecond = 1f;

    [Header("Visual Feedback")]
    public SpriteRenderer playerRenderer;
    public Color baseColor = new Color(0.5f, 0.5f, 0.5f);
    public float flickerSpeed = 12f;
    public float maxScalePulse = 0.2f;
    public float pulseSpeed = 10f;

    [Header("Death")]
    public bool destroyOnDeath = true;
    public float destroyDelay = 0.15f;

    private int blackContacts;
    private int whiteContacts;
    private float blackTimer;
    private float whiteTimer;
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
    }

    void Update()
    {
        if (isDead)
        {
            return;
        }

        float dt = Time.deltaTime;

        if (blackContacts > 0)
        {
            blackTimer += dt;
        }
        else
        {
            blackTimer = Mathf.Max(0f, blackTimer - recoveryPerSecond * dt);
        }

        if (whiteContacts > 0)
        {
            whiteTimer += dt;
        }
        else
        {
            whiteTimer = Mathf.Max(0f, whiteTimer - recoveryPerSecond * dt);
        }

        float blackDanger = maxBlackTime > 0f ? Mathf.Clamp01(blackTimer / maxBlackTime) : 0f;
        float whiteDanger = maxWhiteTime > 0f ? Mathf.Clamp01(whiteTimer / maxWhiteTime) : 0f;
        float danger = Mathf.Max(blackDanger, whiteDanger);

        UpdateVisualFeedback(danger, blackDanger, whiteDanger);

        if (blackTimer >= maxBlackTime || whiteTimer >= maxWhiteTime)
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

    private void UpdateVisualFeedback(float danger, float blackDanger, float whiteDanger)
    {
        float inDanger = (blackContacts > 0 || whiteContacts > 0) ? 1f : 0f;

        if (playerRenderer != null)
        {
            Color targetColor = baseColor;
            if (blackDanger >= whiteDanger && blackContacts > 0)
            {
                targetColor = Color.black;
            }
            else if (whiteContacts > 0)
            {
                targetColor = Color.white;
            }

            float flicker = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;
            float blend = flicker * danger * inDanger;
            playerRenderer.color = Color.Lerp(baseColor, targetColor, blend);
        }

        float pulse = Mathf.Sin(Time.time * pulseSpeed) * maxScalePulse * danger * inDanger;
        transform.localScale = baseScale * (1f + pulse);
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
