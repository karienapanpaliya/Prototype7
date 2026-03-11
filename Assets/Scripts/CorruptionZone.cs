using UnityEngine;

public class CorruptionZone : MonoBehaviour
{
    public enum ZoneType
    {
        Black,
        White
    }

    public ZoneType zoneType = ZoneType.Black;
    public int zonePriority = 0;
}
