using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EndConditionZone : MonoBehaviour
{
    [Header("Behavior")]
    public bool clearCorruptionOnEnter = true;
    public bool disableCorruptionWhileInside = true;
    public bool resumeOnExit = false;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMove playerMove = other.GetComponent<PlayerMove>();
        if (playerMove == null)
        {
            playerMove = other.GetComponentInParent<PlayerMove>();
        }

        PlayerCorruption corruption = other.GetComponent<PlayerCorruption>();
        if (corruption == null)
        {
            corruption = other.GetComponentInParent<PlayerCorruption>();
        }

        if (playerMove != null)
        {
            playerMove.SetPaused(true);
        }

        if (corruption != null)
        {
            if (clearCorruptionOnEnter)
            {
                corruption.ResetCorruption();
            }

            if (disableCorruptionWhileInside)
            {
                corruption.SetSafeMode(true);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerMove playerMove = other.GetComponent<PlayerMove>();
        if (playerMove == null)
        {
            playerMove = other.GetComponentInParent<PlayerMove>();
        }

        if (playerMove != null)
        {
            // Keep this sticky in case overlapping triggers try to unpause.
            playerMove.SetPaused(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!resumeOnExit)
        {
            return;
        }

        PlayerMove playerMove = other.GetComponent<PlayerMove>();
        if (playerMove == null)
        {
            playerMove = other.GetComponentInParent<PlayerMove>();
        }

        PlayerCorruption corruption = other.GetComponent<PlayerCorruption>();
        if (corruption == null)
        {
            corruption = other.GetComponentInParent<PlayerCorruption>();
        }

        if (playerMove != null)
        {
            playerMove.SetPaused(false);
        }

        if (corruption != null && disableCorruptionWhileInside)
        {
            corruption.SetSafeMode(false);
        }
    }
}
