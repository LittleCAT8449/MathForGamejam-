using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this component to a Unity UI Button to play the common click sound.
/// The listener is registered in code, so the Button OnClick list does not
/// need to be edited manually.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonAudio : MonoBehaviour
{
    [SerializeField] private GameAudioManager audioManager;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (audioManager == null)
        {
            audioManager = GameAudioManager.Instance;
        }
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(PlayClickSound);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }
    }

    private void PlayClickSound()
    {
        if (audioManager == null)
        {
            audioManager = GameAudioManager.Instance;
        }

        audioManager?.PlayUIButtonClick();
    }
}
