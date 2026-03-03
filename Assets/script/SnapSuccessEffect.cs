using UnityEngine;

public class SnapSuccessEffect : MonoBehaviour
{
    [Header("Particle Effect")]
    public GameObject particleEffectPrefab;
    public float particleLifetime = 2f;
    
    [Header("Flash Effect")]
    public bool useFlashEffect = true;
    public Color flashColor = Color.green;
    public float flashDuration = 0.3f;
    
    private SnapToPlace snapToPlace;
    
    void Awake()
    {
        snapToPlace = GetComponent<SnapToPlace>();
        if (snapToPlace != null)
        {
            snapToPlace.OnObjectSnappedEvent += HandleSnapSuccess;
        }
    }
    
    void OnDestroy()
    {
        if (snapToPlace != null)
        {
            snapToPlace.OnObjectSnappedEvent -= HandleSnapSuccess;
        }
    }
    
    private void HandleSnapSuccess(GameObject snappedObject, SnapToPlace snapZone)
    {
        if (snapZone != this.snapToPlace)
            return;
        
        if (InventoryAudioManager.Instance != null)
            InventoryAudioManager.Instance.PlaySnapSuccess();
        
        if (particleEffectPrefab != null)
        {
            GameObject particles = Instantiate(particleEffectPrefab, snapZone.transform.position, Quaternion.identity);
            Destroy(particles, particleLifetime);
        }
        
        if (useFlashEffect && snapZone.zoneRenderer != null)
        {
            StartCoroutine(FlashEffect(snapZone.zoneRenderer));
        }
    }
    
    private System.Collections.IEnumerator FlashEffect(Renderer renderer)
    {
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(mpb);
        
        int emissionId = Shader.PropertyToID("_EmissionColor");
        Color originalEmission = mpb.GetColor(emissionId);
        
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            Color currentColor = Color.Lerp(flashColor * 5f, originalEmission, t);
            mpb.SetColor(emissionId, currentColor);
            renderer.SetPropertyBlock(mpb);
            yield return null;
        }
        
        mpb.SetColor(emissionId, originalEmission);
        renderer.SetPropertyBlock(mpb);
    }
}
