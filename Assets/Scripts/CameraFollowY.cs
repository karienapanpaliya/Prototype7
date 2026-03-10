using UnityEngine;

public class CameraFollowY : MonoBehaviour
{
    public Transform target;
    public float yOffset = 2f;
    public float smoothTime = 0.2f;
    public bool onlyMoveUp = true;

    private float yVelocity;

    void Awake()
    {
        TryResolveTarget();
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

    void LateUpdate()
    {
        if (target == null)
        {
            TryResolveTarget();
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

        float smoothedY = Mathf.SmoothDamp(current.y, targetY, ref yVelocity, smoothTime);
        transform.position = new Vector3(current.x, smoothedY, current.z);
    }
}
