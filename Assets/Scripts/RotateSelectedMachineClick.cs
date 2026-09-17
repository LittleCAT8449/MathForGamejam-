using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this component to a SpriteObject with a Collider2D. Clicking it
/// rotates the machine currently selected by MiningMachineWaitingArea.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RotateSelectedMachineClick : MonoBehaviour
{
    [SerializeField] private MiningMachineWaitingArea waitingArea;
    [SerializeField] private Camera inputCamera;

    private Collider2D buttonCollider;
    private bool pointerDownOnButton;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider2D>();

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
        if (GameResetClick.IsModalOpen || Mouse.current == null ||
            inputCamera == null || buttonCollider == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Vector2 screenPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pointerDownOnButton = IsPointerOverButton(screenPosition);
        }

        if (!pointerDownOnButton || !mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        pointerDownOnButton = false;
        if (!IsPointerOverButton(screenPosition))
        {
            return;
        }

        if (waitingArea == null)
        {
            waitingArea = FindFirstObjectByType<MiningMachineWaitingArea>();
        }

        if (waitingArea == null)
        {
            Debug.LogWarning($"{name}：没有找到 MiningMachineWaitingArea。", this);
            return;
        }

        waitingArea.RotateSelectedMachine();
    }

    private bool IsPointerOverButton(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == buttonCollider ||
                hit.collider != null &&
                hit.collider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }
}
