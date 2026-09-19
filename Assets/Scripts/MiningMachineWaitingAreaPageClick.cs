using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// World-space page control for MiningMachineWaitingArea.
/// Attach this component to a Sprite/Object with a Collider2D. It uses a 2D
/// raycast, so it does not require a Canvas or a Unity UI Button.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class MiningMachineWaitingAreaPageClick : MonoBehaviour
{
    public enum PageDirection
    {
        Previous,
        Next
    }

    [Header("翻页设置")]
    [SerializeField] private MiningMachineWaitingArea waitingArea;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private PageDirection direction = PageDirection.Next;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;

    private Collider2D buttonCollider;
    private bool pointerDownOnButton;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider2D>();

        // Keep the object's own layer in the mask so a custom layer mask does
        // not accidentally make this world button impossible to click.
        clickableLayers |= 1 << gameObject.layer;

        if (waitingArea == null)
        {
            waitingArea = FindFirstObjectByType<MiningMachineWaitingArea>();
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null || buttonCollider == null)
        {
            pointerDownOnButton = false;
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerDownOnButton = IsPointerOverButton(pointerPosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bool clickedButton = pointerDownOnButton && IsPointerOverButton(pointerPosition);
            pointerDownOnButton = false;

            if (clickedButton)
            {
                CallPage();
            }
        }
    }

    /// <summary>
    /// Public entry point for other world-object scripts or UnityEvents.
    /// </summary>
    public void CallPage()
    {
        if (GameResetClick.IsModalOpen)
        {
            return;
        }

        if (waitingArea == null)
        {
            waitingArea = FindFirstObjectByType<MiningMachineWaitingArea>();
        }

        if (waitingArea == null)
        {
            Debug.LogWarning($"{name}：没有找到 MiningMachineWaitingArea，无法翻页。", this);
            return;
        }

        GameAudioManager.Instance?.PlayUIButtonClick();

        if (direction == PageDirection.Next)
        {
            waitingArea.NextPage();
        }
        else
        {
            waitingArea.PreviousPage();
        }
    }

    private bool IsPointerOverButton(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, clickableLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == buttonCollider || hitCollider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }
}
