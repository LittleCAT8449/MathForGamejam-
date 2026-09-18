using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays a sound when a Sprite/Collider2D button is clicked.
/// Use the default sound for normal scene buttons. The press lever keeps its
/// own StampingMachineMoveClick component because it needs to start the press.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldButtonAudio : MonoBehaviour
{
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;

    private Collider2D buttonCollider;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider2D>();
        clickableLayers |= 1 << gameObject.layer;

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null ||
            Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = inputCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(
            ray,
            Mathf.Infinity,
            clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == buttonCollider ||
                (hit.collider != null && hit.collider.transform.IsChildOf(transform)))
            {
                GameAudioManager.Instance?.PlayUIButtonClick();
                return;
            }
        }
    }
}
