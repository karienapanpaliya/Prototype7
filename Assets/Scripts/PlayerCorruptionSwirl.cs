using UnityEngine;

// This inherits EVERYTHING (timers, particles, death logic) from your partner's script
public class PlayerCorruptionSwirl : PlayerCorruption 
{
    [Header("Swirl Detection")]
    public SpriteRenderer swirlRenderer;

    // We ONLY change this one specific part of the logic
    protected override ActiveZone ResolveActiveZone()
    {
        if (swirlRenderer == null || swirlRenderer.sprite == null)
        {
            return ActiveZone.None;
        }

        Texture2D tex = swirlRenderer.sprite.texture;
        
        // Convert player world position to texture UV coordinates
        Vector2 localPos = swirlRenderer.transform.InverseTransformPoint(transform.position);
        float u = (localPos.x / swirlRenderer.bounds.size.x) + 0.5f;
        float v = (localPos.y / swirlRenderer.bounds.size.y) + 0.5f;

        // If player is outside the background, they are safe (None)
        if (u < 0 || u > 1 || v < 0 || v > 1) return ActiveZone.None;

        // Get the color from the texture
        float brightness = tex.GetPixelBilinear(u, v).grayscale;
        
        // If brightness is high, it's White. Otherwise, it's Black.
        return brightness > 0.5f ? ActiveZone.White : ActiveZone.Black;
    }
    
    private ActiveZone previousFrameZone;

protected override void Update()
{
    ActiveZone currentPixelZone = ResolveActiveZone();

    // If the pixel color changed since the last frame
    if (currentPixelZone != previousFrameZone && currentPixelZone != ActiveZone.None)
    {
        // If we were already in a zone (meaning this is a SWAP, not just starting)
        if (previousFrameZone != ActiveZone.None) 
        {
            // 1. Reset the timers
            zoneTimer = 0f; 
            hasBecomeUnsafe = false;
            
            // 2. Reset the "Progress" bars that control flickering/zooming
            currentExposureProgress = 0f;
            currentDangerProgress = 0f;
            currentSafeWindowProgress = 0f; // Reset the safe window too!

            // 3. Update the Gate positions
            lastCleanSwapTime = Time.time;
            lastCleanSwapPosition = transform.position;

            // 4. Update the actual zone variables
            activeZone = currentPixelZone;
            lastCommittedZone = currentPixelZone;

            Debug.Log($"<color=cyan>Resetting everything!</color> Swapped to: {currentPixelZone}");
        }
    }

    previousFrameZone = currentPixelZone;

    // Run the parent logic AFTER we've reset the timers
    base.Update();
}

}