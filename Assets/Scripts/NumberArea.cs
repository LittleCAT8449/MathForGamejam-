using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the number area's top-left anchor. Spawned number prefabs are laid
/// out in rows using local positions, so the area can be moved as one object.
/// </summary>
public class NumberArea : MonoBehaviour
{
    [SerializeField] private NumberToken numberPrefab;
    [SerializeField, Min(1)] private int columns = 5;
    [SerializeField] private Vector2 spacing = new Vector2(1f, 1f);
    [SerializeField] private Vector2 firstTokenLocalPosition = Vector2.zero;
    [SerializeField] private CameraSmoothMove cameraMover;
    [SerializeField] private Vector2 cameraMoveTargetWorldPosition;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Collider2D stampingArea;
    [SerializeField] private SettlementArea settlementArea;
    [SerializeField, Min(0f)] private float clickThresholdPixels = 8f;

    private readonly List<NumberToken> spawnedTokens = new List<NumberToken>();
    private bool pointerPressed;
    private Vector2 pointerDownPosition;

    private void Awake()
    {
        if (cameraMover == null)
        {
            cameraMover = FindFirstObjectByType<CameraSmoothMove>();
        }

        if (inputCamera == null)
        {
            inputCamera = cameraMover != null
                ? cameraMover.GetComponent<Camera>()
                : Camera.main;
        }

        if (settlementArea == null)
        {
            settlementArea = FindFirstObjectByType<SettlementArea>();
        }
    }

    private void Update()
    {
        if (GameResetClick.IsModalOpen || inputCamera == null ||
            stampingArea == null || Mouse.current == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Vector2 screenPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pointerPressed = true;
            pointerDownPosition = screenPosition;
        }

        if (!pointerPressed || !mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        pointerPressed = false;
        if ((screenPosition - pointerDownPosition).sqrMagnitude >
            clickThresholdPixels * clickThresholdPixels)
        {
            return;
        }

        HandleStampingAreaClick(screenPosition);
        HandleSettlementAreaClick(screenPosition);
    }

    /// <summary>
    /// Creates and positions one number token. Returns null if no prefab is set.
    /// </summary>
    public NumberToken SpawnNumber(decimal value)
    {
        if (numberPrefab == null)
        {
            Debug.LogError($"{name}：Number Prefab 未设置，无法生成数字物体。", this);
            return null;
        }

        int index = spawnedTokens.Count;
        int safeColumns = Mathf.Max(1, columns);
        int column = index % safeColumns;
        int row = index / safeColumns;

        Vector3 localPosition = new Vector3(
            firstTokenLocalPosition.x + column * spacing.x,
            firstTokenLocalPosition.y - row * spacing.y,
            0f);
        Vector3 worldPosition = transform.TransformPoint(localPosition);

        // Instantiate unparented first, then preserve its world transform when
        // parenting. This keeps the prefab's configured world size even when
        // the number-area object has a non-unit scale.
        NumberToken token = Instantiate(numberPrefab, worldPosition, transform.rotation);
        token.transform.SetParent(transform, true);
        token.SetValue(value);

        NumberTokenDrag drag = token.GetComponent<NumberTokenDrag>();
        if (drag == null)
        {
            drag = token.gameObject.AddComponent<NumberTokenDrag>();
        }

        // Keep the settlement reference on every token created by this area,
        // so the original drag-and-drop path can still deliver it there.
        drag.Configure(inputCamera, stampingArea, settlementArea);
        spawnedTokens.Add(token);

        if (cameraMover != null)
        {
            cameraMover.MoveTo(cameraMoveTargetWorldPosition);
        }

        Debug.Log($"数字区生成数字：{value}。", token);
        return token;
    }

    /// <summary>
    /// Removes all tokens created by this area, including tokens moved elsewhere.
    /// </summary>
    public void ClearNumbers()
    {
        foreach (NumberToken token in spawnedTokens)
        {
            if (token != null)
            {
                Destroy(token.gameObject);
            }
        }

        spawnedTokens.Clear();
    }

    private void HandleStampingAreaClick(Vector2 screenPosition)
    {
        if (!TryGetPointerWorldPosition(screenPosition, out Vector3 worldPosition) ||
            !stampingArea.OverlapPoint(worldPosition))
        {
            return;
        }

        NumberToken clickedToken = FindTopmostToken(screenPosition);
        if (clickedToken != null)
        {
            ReturnNumberToNumberArea(clickedToken);
            return;
        }

        // The press button can sit inside the stamping-area collider. Let its
        // own click script receive that click instead of treating the button
        // as a free placement point when a number is selected.
        if (IsSceneActionClick(screenPosition))
        {
            return;
        }

        NumberToken selectedToken = NumberToken.SelectedToken;
        if (selectedToken == null)
        {
            return;
        }

        MoveSelectedNumberToStampingArea(selectedToken, worldPosition);
    }

    private void HandleSettlementAreaClick(Vector2 screenPosition)
    {
        if (settlementArea == null ||
            !TryGetPointerWorldPosition(screenPosition, out Vector3 worldPosition) ||
            !settlementArea.ContainsPoint(worldPosition))
        {
            return;
        }

        NumberToken clickedToken = FindTopmostToken(screenPosition);
        if (clickedToken != null)
        {
            ReturnNumberToNumberArea(clickedToken);
        }
    }

    private void MoveSelectedNumberToStampingArea(
        NumberToken token,
        Vector3 worldPosition)
    {
        if (token == null)
        {
            return;
        }

        if (settlementArea != null)
        {
            settlementArea.RemoveNumber(token);
        }

        NumberTokenDrag drag = token.GetComponent<NumberTokenDrag>();
        if (drag == null)
        {
            drag = token.gameObject.AddComponent<NumberTokenDrag>();
        }

        drag.Configure(inputCamera, stampingArea, settlementArea);
        drag.MoveToStampingArea(worldPosition);
        token.SetSelected(false);
        Debug.Log($"数字 {token.Value} 已通过点击放入冲压区。", token);
    }

    public void ReturnNumberToNumberArea(NumberToken token)
    {
        if (token == null)
        {
            return;
        }

        if (NumberToken.SelectedToken != null)
        {
            NumberToken.SelectedToken.SetSelected(false);
        }

        NumberTokenDrag drag = token.GetComponent<NumberTokenDrag>();
        if (drag != null)
        {
            drag.CancelPointerInteraction();
        }

        if (settlementArea != null)
        {
            settlementArea.RemoveNumber(token);
        }

        spawnedTokens.RemoveAll(number => number == null);
        if (!spawnedTokens.Contains(token))
        {
            spawnedTokens.Add(token);
        }

        int index = spawnedTokens.IndexOf(token);
        int safeColumns = Mathf.Max(1, columns);
        Vector3 localPosition = new Vector3(
            firstTokenLocalPosition.x + (index % safeColumns) * spacing.x,
            firstTokenLocalPosition.y - (index / safeColumns) * spacing.y,
            0f);
        Vector3 worldPosition = transform.TransformPoint(localPosition);

        if (drag == null)
        {
            drag = token.gameObject.AddComponent<NumberTokenDrag>();
        }

        drag.Configure(inputCamera, stampingArea, settlementArea);
        drag.MoveToNumberArea(transform, worldPosition);
        token.SetSelected(false);
        Debug.Log($"交付区数字 {token.Value} 已返回数字区。", token);
    }

    private bool TryGetPointerWorldPosition(
        Vector2 screenPosition,
        out Vector3 worldPosition)
    {
        worldPosition = default;
        if (inputCamera == null)
        {
            return false;
        }

        float depth = inputCamera.WorldToScreenPoint(transform.position).z;
        worldPosition = inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = transform.position.z;
        return true;
    }

    private NumberToken FindTopmostToken(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);

        NumberToken nearestToken = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in hits)
        {
            NumberToken token = hit.collider.GetComponentInParent<NumberToken>();
            if (token != null && hit.distance < nearestDistance)
            {
                nearestToken = token;
                nearestDistance = hit.distance;
            }
        }

        return nearestToken;
    }

    private bool IsSceneActionClick(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<StampingMachineMoveClick>() != null ||
                hit.collider.GetComponentInParent<StampingMachine>() != null ||
                hit.collider.GetComponentInParent<StartMiningObject>() != null ||
                hit.collider.GetComponentInParent<GameResetClick>() != null ||
                hit.collider.GetComponentInParent<RotateSelectedMachineClick>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        spacing.x = Mathf.Max(0.001f, spacing.x);
        spacing.y = Mathf.Max(0.001f, spacing.y);
    }
}
