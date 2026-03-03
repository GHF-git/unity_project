using UnityEngine;

public class InventoryAudioManager : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip dragStartSound;
    public AudioClip dragEndSound;
    public AudioClip snapSuccessSound;
    public AudioClip snapFailSound;
    public AudioClip itemRespawnSound;
    
    [Header("Settings")]
    public float volume = 1f;
    
    private AudioSource audioSource;
    private static InventoryAudioManager instance;
    
    public static InventoryAudioManager Instance
    {
        get { return instance; }
    }
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
    }
    
    public void PlayDragStart()
    {
        PlaySound(dragStartSound);
    }
    
    public void PlayDragEnd()
    {
        PlaySound(dragEndSound);
    }
    
    public void PlaySnapSuccess()
    {
        PlaySound(snapSuccessSound);
    }
    
    public void PlaySnapFail()
    {
        PlaySound(snapFailSound);
    }
    
    public void PlayItemRespawn()
    {
        PlaySound(itemRespawnSound);
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
