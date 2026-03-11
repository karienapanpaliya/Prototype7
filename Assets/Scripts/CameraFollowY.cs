using UnityEngine;

public class CameraFollowY : MonoBehaviour
{
    public Transform target;
    public float yOffset = 2f;
    public float smoothTime = 0.08f;
    public float fastCatchupSmoothTime = 0.01f;
    public float catchupDistance = 0.5f;
    public bool onlyMoveUp = true;

    [Header("Danger Zoom")]
    public bool enableDangerZoom = true;
    public float maxZoomIn = 0.9f;
    public float zoomSmoothTime = 0.12f;
    public float minPulseSpeed = 0.8f;
    public float maxPulseSpeed = 4.5f;
    public float maxPulseAmplitude = 0.2f;

    private float yVelocity;
    private float zoomVelocity;
    private Camera cachedCamera;
    private PlayerCorruption playerCorruption;
    private float baseOrthographicSize;

    void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera != null)
        {
            baseOrthographicSize = cachedCamera.orthographicSize;
        }

        TryResolveTarget();
        TryResolveCorruption();
    }

    private void TryResolveTarget()
    {
        if (target != null)
        {
            return;
        }

        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null)
        {
            target = playerMove.transform;
            return;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            target = taggedPlayer.transform;
        }
    }

    private void TryResolveCorruption()
    {
        if (playerCorruption != null)
        {
            return;
        }

        if (target != null)
        {
            playerCorruption = target.GetComponent<PlayerCorruption>();
        }

        if (playerCorruption == null)
        {
            playerCorruption = FindFirstObjectByType<PlayerCorruption>();
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            TryResolveTarget();
        }

        if (playerCorruption == null)
        {
            TryResolveCorruption();
        }

        if (target == null)
        {
            Debug.LogWarning("CameraFollowY has no target. Assign the player Transform or tag player as 'Player'.", this);
            return;
        }

        Vector3 current = transform.position;
        float targetY = target.position.y + yOffset;

        if (onlyMoveUp)
        {
            targetY = Mathf.Max(current.y, targetY);
        }

        float yDelta = Mathf.Abs(targetY - current.y);
        float activeSmoothTime = yDelta > catchupDistance
            ? fastCatchupSmoothTime
            : smoothTime;

        float smoothedY = Mathf.SmoothDamp(current.y, targetY, ref yVelocity, activeSmoothTime);
        transform.position = new Vector3(current.x, smoothedY, current.z);

        UpdateDangerZoom();
    }

    private void UpdateDangerZoom()
    {
        if (!enableDangerZoom || cachedCamera == null || !cachedCamera.orthographic)
        {
            return;
        }

        float anticipation = 0f;
        if (playerCorruption != null)
        {
            anticipation = Mathf.Max(playerCorruption.ExposureProgress * 0.65f, playerCorruption.DangerProgress);
        }

        float pulse = 0f;
        if (anticipation > 0f)
        {
            float pulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, anticipation);
            pulse = ((Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f) * maxPulseAmplitude * anticipation;
        }

        float zoomOffset = (maxZoomIn * anticipation) + pulse;
        float targetSize = Mathf.Max(0.01f, baseOrthographicSize - zoomOffset);
        cachedCamera.orthographicSize = Mathf.SmoothDamp(
            cachedCamera.orthographicSize,
            targetSize,
            ref zoomVelocity,
            zoomSmoothTime);
    }
}
