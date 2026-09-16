using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to a clickable 2D object to call StampingMachine.Move().
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StampingMachineMoveClick : MonoBehaviour
{
    [SerializeField] private StampingMachine stampingMachine;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask clickableLayers = Physics2D.DefaultRaycastLayers;

    private void Awake()
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (stampingMachine == null)
        {
            stampingMachine = FindFirstObjectByType<StampingMachine>();
        }
    }

    private void Start()
    {
        if (inputCamera == null)
        {
            Debug.LogWarning($"{name}：没有找到输入摄像机，请设置 Input Camera 或将摄像机 Tag 设为 MainCamera。", this);
        }

        if (stampingMachine == null)
        {
            Debug.LogWarning($"{name}：没有找到 StampingMachine，请在 Inspector 中指定。", this);
        }
    }

    private void Update()
    {
        if (inputCamera == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (WasClicked(Mouse.current.position.ReadValue()))
        {
            CallMove();
        }
    }

    /// <summary>
    /// Can also be connected to a Unity UI Button's OnClick event.
    /// </summary>
    public void CallMove()
    {
        if (stampingMachine == null)
        {
            Debug.LogWarning("无法启动冲压：没有指定 StampingMachine。", this);
            return;
        }

        stampingMachine.Move();
    }

    private bool WasClicked(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, clickableLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<StampingMachineMoveClick>() == this)
            {
                return true;
            }
        }

        return false;
    }
}
