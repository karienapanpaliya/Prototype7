using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class EndCoreGate : MonoBehaviour
{
    [Header("Gate Rule")]
    public float swapMustBeWithinDistance = 3f;
    public float maxSecondsAfterSwap = 4f;
    public bool requireStablePlayer = true;

    [Header("Visuals")]
    public float halfWidth = 0.9f;
    public float halfHeight = 2.2f;
    public float crackGap = 0.08f;
    public float crackedTilt = 8f;
    public float fuseDuration = 0.9f;
    public Color deadBlack = new Color(0.09f, 0.09f, 0.1f, 1f);
    public Color deadWhite = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color fusedColor = new Color(1f, 0.38f, 0.2f, 1f);

    [Header("Open Path")]
    public GameObject pathBlocker;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private SpriteRenderer leftHalf;
    private SpriteRenderer rightHalf;
    private SpriteRenderer fusedCore;
    private bool isSolved;

    private static Sprite rectangleSprite;

    void Awake()
    {
        EnsureVisuals();
        EnsureTrigger();
    }

    private void EnsureTrigger()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2((halfWidth * 2f) + 1.2f, (halfHeight * 2f) + 0.8f);
    }

    private void EnsureVisuals()
    {
        if (leftHalf != null && rightHalf != null && fusedCore != null)
        {
            return;
        }

        Sprite sprite = GetRectangleSprite();

        Transform left = transform.Find("CoreLeft");
        if (left == null)
        {
            GameObject leftObj = new GameObject("CoreLeft");
            leftObj.transform.SetParent(transform, false);
            left = leftObj.transform;
        }

        Transform right = transform.Find("CoreRight");
        if (right == null)
        {
            GameObject rightObj = new GameObject("CoreRight");
            rightObj.transform.SetParent(transform, false);
            right = rightObj.transform;
        }

        Transform fused = transform.Find("CoreFused");
        if (fused == null)
        {
            GameObject fusedObj = new GameObject("CoreFused");
            fusedObj.transform.SetParent(transform, false);
            fused = fusedObj.transform;
        }

        leftHalf = EnsureRenderer(left, sprite, 20);
        rightHalf = EnsureRenderer(right, sprite, 21);
        fusedCore = EnsureRenderer(fused, sprite, 22);

        leftHalf.transform.localPosition = new Vector3(-(crackGap * 0.5f), 0f, 0f);
        rightHalf.transform.localPosition = new Vector3(crackGap * 0.5f, 0f, 0f);
        leftHalf.transform.localRotation = Quaternion.Euler(0f, 0f, crackedTilt);
        rightHalf.transform.localRotation = Quaternion.Euler(0f, 0f, -crackedTilt);

        leftHalf.transform.localScale = new Vector3(halfWidth, halfHeight, 1f);
        rightHalf.transform.localScale = new Vector3(halfWidth, halfHeight, 1f);

        leftHalf.color = deadBlack;
        rightHalf.color = deadWhite;

        fusedCore.transform.localPosition = Vector3.zero;
        fusedCore.transform.localRotation = Quaternion.identity;
        fusedCore.transform.localScale = new Vector3(halfWidth * 2f, halfHeight, 1f);
        fusedCore.color = new Color(fusedColor.r, fusedColor.g, fusedColor.b, 0f);
        fusedCore.enabled = false;
    }

    private static SpriteRenderer EnsureRenderer(Transform target, Sprite sprite, int sortingOrder)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = target.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Sprite GetRectangleSprite()
    {
        if (rectangleSprite != null)
        {
            return rectangleSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        rectangleSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return rectangleSprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isSolved)
        {
            return;
        }

        PlayerCorruption corruption = other.GetComponent<PlayerCorruption>();
        if (corruption == null)
        {
            corruption = other.GetComponentInParent<PlayerCorruption>();
        }

        if (corruption == null)
        {
            return;
        }

        if (!MeetsSolveCondition(corruption))
        {
            return;
        }

        StartCoroutine(SolveGateRoutine());
    }

    private bool MeetsSolveCondition(PlayerCorruption corruption)
    {
        if (requireStablePlayer && !corruption.IsStable)
        {
            DebugLog("Touch rejected: player is not stable.");
            return false;
        }

        float distanceFromGateAtSwap = Vector2.Distance(corruption.LastCleanSwapPosition, transform.position);
        if (distanceFromGateAtSwap > swapMustBeWithinDistance)
        {
            DebugLog("Touch rejected: clean swap was too far from gate (" + distanceFromGateAtSwap.ToString("F2") + ").");
            return false;
        }

        float secondsSinceSwap = Time.time - corruption.LastCleanSwapTime;
        if (secondsSinceSwap > maxSecondsAfterSwap)
        {
            DebugLog("Touch rejected: clean swap was too old (" + secondsSinceSwap.ToString("F2") + "s).");
            return false;
        }

        DebugLog("Gate solve accepted.");
        return true;
    }

    private IEnumerator SolveGateRoutine()
    {
        isSolved = true;

        Vector3 leftStartPos = leftHalf.transform.localPosition;
        Vector3 rightStartPos = rightHalf.transform.localPosition;
        Quaternion leftStartRot = leftHalf.transform.localRotation;
        Quaternion rightStartRot = rightHalf.transform.localRotation;

        Vector3 leftTargetPos = new Vector3(-0.5f, 0f, 0f);
        Vector3 rightTargetPos = new Vector3(0.5f, 0f, 0f);
        Quaternion targetRot = Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < fuseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fuseDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            leftHalf.transform.localPosition = Vector3.Lerp(leftStartPos, leftTargetPos, eased);
            rightHalf.transform.localPosition = Vector3.Lerp(rightStartPos, rightTargetPos, eased);
            leftHalf.transform.localRotation = Quaternion.Slerp(leftStartRot, targetRot, eased);
            rightHalf.transform.localRotation = Quaternion.Slerp(rightStartRot, targetRot, eased);

            leftHalf.color = Color.Lerp(deadBlack, fusedColor, eased * 0.8f);
            rightHalf.color = Color.Lerp(deadWhite, fusedColor, eased * 0.8f);

            yield return null;
        }

        leftHalf.enabled = false;
        rightHalf.enabled = false;

        fusedCore.enabled = true;
        fusedCore.color = new Color(fusedColor.r, fusedColor.g, fusedColor.b, 1f);
        fusedCore.transform.localScale = new Vector3(halfWidth * 1.6f, halfHeight * 0.95f, 1f);

        yield return new WaitForSeconds(0.08f);
        fusedCore.transform.localScale = new Vector3(halfWidth * 2f, halfHeight, 1f);

        if (pathBlocker != null)
        {
            pathBlocker.SetActive(false);
        }

        DebugLog("Gate solved: path opened.");
    }

    private void DebugLog(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log("[EndCoreGate] " + message, this);
    }
}
