using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this component to the stamping machine root. Clicking the machine
/// or any of its collider children activates the assigned GameObject.
/// </summary>
public class StampingMachineClickActivate : MonoBehaviour
{
    [SerializeField] private GameObject targetObject;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;

    private Collider2D[] machineColliders;

    private void Awake()
    {
        machineColliders = GetComponentsInChildren<Collider2D>(true);

        // Include every collider layer used by the machine, including child
        // colliders, so a custom layer does not silently disable the click.
        clickableLayers |= 1 << gameObject.layer;
        foreach (Collider2D machineCollider in machineColliders)
        {
            if (machineCollider != null)
            {
                clickableLayers |= 1 << machineCollider.gameObject.layer;
            }
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    private void Start()
    {
        if (inputCamera == null)
        {
            Debug.LogWarning(
                $"{name}：没有找到输入摄像机，请设置 Input Camera 或将摄像机 Tag 设为 MainCamera。",
                this);
        }

        if (machineColliders == null || machineColliders.Length == 0)
        {
            Debug.LogWarning(
                $"{name}：冲压机没有 Collider2D，无法接收点击。",
                this);
        }

        if (targetObject == null)
        {
            Debug.LogWarning(
                $"{name}：没有指定 Target Object，点击冲压机时不会激活对象。",
                this);
        }
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (WasClicked(Mouse.current.position.ReadValue()))
        {
            ActivateTarget();
        }
    }

    /// <summary>
    /// Activates the configured object. This can also be connected to a UI
    /// Button's OnClick event.
    /// </summary>
    public void ActivateTarget()
    {
        if (GameResetClick.IsModalOpen)
        {
            return;
        }

        if (targetObject == null)
        {
            Debug.LogWarning($"{name}：无法激活对象，因为 Target Object 未设置。", this);
            return;
        }

        targetObject.SetActive(true);
        Debug.Log($"点击冲压机，已激活对象：{targetObject.name}。", targetObject);
    }

    private bool WasClicked(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(
            ray,
            Mathf.Infinity,
            clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null &&
                hit.collider.GetComponentInParent<StampingMachineClickActivate>() == this)
            {
                return true;
            }
        }

        return false;
    }
}
