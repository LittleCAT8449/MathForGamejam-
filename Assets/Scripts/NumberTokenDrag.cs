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
    [SerializeField, Min(0f)] private float dragThresholdPixels = 8f;

    private NumberToken numberToken;
    private SettlementArea settlementArea;
    private Vector3 originalPosition;
    private Vector3 dragOffset;
    private float pointerDepth;
    private Vector2 pointerDownPosition;
    private bool pointerPressed;
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

    /// <summary>
    /// Moves this number to a clicked point in the stamping area and enables
    /// the same dynamic physics used by normal drag-and-drop.
    /// </summary>
    public void MoveToStampingArea(Vector3 worldPosition)
    {
        pointerPressed = false;
        isDragging = false;
        transform.SetParent(null, true);
        worldPosition.z = transform.position.z;
        transform.position = worldPosition;
        EnableDynamicPhysics();
    }

    public bool IsPointerPressed => pointerPressed;

    /// <summary>
    /// Cancels the current pointer interaction. This is used when the area
    /// click handler consumes a click on a stamping-area token before the
    /// normal drag handler processes the same mouse release.
    /// </summary>
    public void CancelPointerInteraction()
    {
        pointerPressed = false;
        isDragging = false;
    }

    /// <summary>
    /// Places this number back into the number area at the supplied world
    /// position and turns off gravity so it stays in its slot.
    /// </summary>
    public void MoveToNumberArea(Transform numberAreaTransform, Vector3 worldPosition)
    {
        pointerPressed = false;
        isDragging = false;
        transform.SetParent(numberAreaTransform, true);
        worldPosition.z = transform.position.z;
        transform.position = worldPosition;
        SettlePhysics();
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null || Mouse.current == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Vector2 pointerPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginPointerInteraction(pointerPosition);
        }

        if (pointerPressed && mouse.leftButton.isPressed && !isDragging &&
            (pointerPosition - pointerDownPosition).sqrMagnitude >=
            dragThresholdPixels * dragThresholdPixels)
        {
            StartDrag();
        }

        if (isDragging && mouse.leftButton.isPressed)
        {
            UpdateDrag(pointerPosition);
        }

        if (pointerPressed && mouse.leftButton.wasReleasedThisFrame)
        {
            if (isDragging)
            {
                EndDrag();
            }
            else
            {
                pointerPressed = false;
                TutorialTooltipController.FindOrCreate()
                    .OnNumberClicked(numberToken);
                numberToken.ToggleSelected();
            }
        }
    }

    private void BeginPointerInteraction(Vector2 screenPosition)
    {
        if (pointerPressed || FindTopmostToken(screenPosition) != this)
        {
            return;
        }

        originalPosition = transform.position;
        pointerDepth = inputCamera.WorldToScreenPoint(originalPosition).z;
        dragOffset = originalPosition - ScreenToWorld(screenPosition);
        pointerDownPosition = screenPosition;
        pointerPressed = true;
    }

    private void StartDrag()
    {
        isDragging = true;
        numberToken.SetSelected(true);

        Debug.Log($"开始拖拽数字：{numberToken.Value}", numberToken);
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        Vector3 position = ScreenToWorld(screenPosition) + dragOffset;
        position.z = originalPosition.z;
        transform.position = position;

        if (stampingArea != null && stampingArea.OverlapPoint(position))
        {
            TutorialTooltipController.FindOrCreate().OnEnteredStampingArea();
        }
    }

    private void EndDrag()
    {
        isDragging = false;
        pointerPressed = false;

        Vector2 dropPosition = transform.position;

        if (settlementArea != null && settlementArea.ContainsPoint(dropPosition))
        {
            settlementArea.PlaceNumber(numberToken);
            SettlePhysics();
            return;
        }

        if (stampingArea != null && stampingArea.OverlapPoint(dropPosition))
        {
            TutorialTooltipController.FindOrCreate().OnEnteredStampingArea();
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
