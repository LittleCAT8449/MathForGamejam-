using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets a generated number token be dragged with the mouse and dropped into
/// the assigned stamping-area collider.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class NumberTokenDrag : MonoBehaviour
{
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Collider2D stampingArea;

    private NumberToken numberToken;
    private SettlementArea settlementArea;
    private Vector3 originalPosition;
    private Vector3 dragOffset;
    private float pointerDepth;
    private bool isDragging;

    private void Awake()
    {
        numberToken = GetComponent<NumberToken>();
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    /// <summary>
    /// Supplies the scene references when this component is added to a spawned token.
    /// </summary>
    public void Configure(
        Camera cameraToUse,
        Collider2D targetStampingArea,
        SettlementArea targetSettlementArea = null)
    {
        if (cameraToUse != null)
        {
            inputCamera = cameraToUse;
        }

        stampingArea = targetStampingArea;
        settlementArea = targetSettlementArea;
    }

    private void Update()
    {
        if (inputCamera == null || Mouse.current == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Vector2 pointerPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginDrag(pointerPosition);
        }

        if (isDragging && mouse.leftButton.isPressed)
        {
            UpdateDrag(pointerPosition);
        }

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        if (isDragging || FindTopmostToken(screenPosition) != this)
        {
            return;
        }

        originalPosition = transform.position;
        pointerDepth = inputCamera.WorldToScreenPoint(originalPosition).z;
        dragOffset = originalPosition - ScreenToWorld(screenPosition);
        isDragging = true;

        Debug.Log($"开始拖拽数字：{numberToken.Value}", numberToken);
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        Vector3 position = ScreenToWorld(screenPosition) + dragOffset;
        position.z = originalPosition.z;
        transform.position = position;
    }

    private void EndDrag()
    {
        isDragging = false;

        Vector2 dropPosition = transform.position;

        if (settlementArea != null && settlementArea.ContainsPoint(dropPosition))
        {
            settlementArea.PlaceNumber(numberToken);
            SettlePhysics();
            return;
        }

        if (stampingArea != null && stampingArea.OverlapPoint(dropPosition))
        {
            EnableDynamicPhysics();
            Debug.Log($"数字 {numberToken.Value} 已放入冲压区。", numberToken);
        }
        else
        {
            transform.position = originalPosition;
            Debug.Log($"数字 {numberToken.Value} 未放入目标区域，已放回原位。", numberToken);
        }
    }

    private void EnableDynamicPhysics()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 1f;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.simulated = true;

        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D tokenCollider in colliders)
        {
            tokenCollider.isTrigger = false;
        }
    }

    private void SettlePhysics()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
    }

    private NumberTokenDrag FindTopmostToken(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);

        NumberTokenDrag topmostToken = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            NumberTokenDrag hitToken = hit.collider.GetComponentInParent<NumberTokenDrag>();
            if (hitToken != null && hit.distance < nearestDistance)
            {
                topmostToken = hitToken;
                nearestDistance = hit.distance;
            }
        }

        return topmostToken;
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        return inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, pointerDepth));
    }
}
