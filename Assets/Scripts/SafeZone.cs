using UnityEngine;

public class SafeZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerCorruption playerCorruption = collision.GetComponent<PlayerCorruption>();
        if (playerCorruption == null)
        {
            playerCorruption = collision.GetComponentInParent<PlayerCorruption>();
        }

        PlayerMove playerMove = collision.GetComponent<PlayerMove>();
        if (playerMove == null)
        {
            playerMove = collision.GetComponentInParent<PlayerMove>();
        }

        if (playerCorruption != null)
        {
            playerCorruption.SetSafeMode(true);
        }

        if (playerMove != null)
        {
            playerMove.SetPaused(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        PlayerCorruption playerCorruption = collision.GetComponent<PlayerCorruption>();
        if (playerCorruption == null)
        {
            playerCorruption = collision.GetComponentInParent<PlayerCorruption>();
        }

        PlayerMove playerMove = collision.GetComponent<PlayerMove>();
        if (playerMove == null)
        {
            playerMove = collision.GetComponentInParent<PlayerMove>();
        }

        if (playerCorruption != null)
        {
            playerCorruption.SetSafeMode(false);
        }

        if (playerMove != null)
        {
            playerMove.SetPaused(false);
        }
    }
}
