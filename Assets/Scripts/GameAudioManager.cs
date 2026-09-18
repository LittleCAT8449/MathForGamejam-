using UnityEngine;

/// <summary>
/// Central audio player for the short game sound effects.
/// Put this component on a persistent scene object (for example Manager),
/// then assign the four AudioClip fields in the Inspector.
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Audio source")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    [Header("Sound effects")]
    [SerializeField] private AudioClip uiButtonClickClip;
    [SerializeField] private AudioClip stampingWorkClip;
    [SerializeField] private AudioClip leverPullClip;
    [SerializeField] private AudioClip placeMiningMachineClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public void PlayUIButtonClick()
    {
        Play(uiButtonClickClip);
    }

    public void PlayStampingWork()
    {
        Play(stampingWorkClip);
    }

    public void PlayLeverPull()
    {
        Play(leverPullClip);
    }

    public void PlayPlaceMiningMachine()
    {
        Play(placeMiningMachineClip);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, masterVolume);
    }
}
