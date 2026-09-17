using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Attach this component to the waiting-area object. Put selectable machine objects
/// underneath it and add MiningMachineItem to each machine.
/// </summary>
public class MiningMachineWaitingArea : MonoBehaviour
{
    [Serializable]
    private class InitialMachineEntry
    {
        [SerializeField] private MiningMachineItem prefab;
        [SerializeField, Min(1)] private int count = 1;

        public MiningMachineItem Prefab => prefab;
        public int Count => Mathf.Max(1, count);
    }

    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask machineLayerMask = Physics2D.DefaultRaycastLayers;
    [SerializeField] private List<MiningMachineItem> machines = new List<MiningMachineItem>();
    [SerializeField] private MiningMachineDeploymentArea deploymentArea;
    [SerializeField, Min(0f)] private float dragThresholdPixels = 8f;

    [Header("部署提示")]
    [Tooltip("拖拽一个 TextMeshPro 或 TextMeshProUGUI 到这里。放置位置无效时会显示提示。")]
    [SerializeField] private TMP_Text deploymentPrompt;
    [SerializeField] private string blockedDeploymentMessage = "区域已占用，无法部署/旋转";

    [Header("开局生成")]
    [SerializeField] private bool spawnInitialMachines = true;
    [SerializeField] private List<InitialMachineEntry> initialMachines =
        new List<InitialMachineEntry>();
    [Tooltip("生成实例的父物体。可指定原来放置 Square 的‘选择物体’；为空时使用待选区域自身。")]
    [SerializeField] private Transform spawnParent;
    [SerializeField] private Vector2 initialStartLocalPosition = Vector2.zero;
    [SerializeField] private Vector2 initialSpacing = new Vector2(1.2f, 1.2f);
    [SerializeField, Min(1)] private int initialColumns = 4;
    [Tooltip("启用后按待选区域 SpriteRenderer 的范围自动排列初始机器，避免 spawnParent 的缩放导致机器跑出区域。")]
    [SerializeField] private bool fitInitialMachinesToWaitingArea = true;
    [SerializeField, Min(0f)] private float initialLayoutPadding = 0.05f;

    private bool initialMachinesSpawned;

    public MiningMachineItem SelectedMachine { get; private set; }

    private MiningMachineItem pressedMachine;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private Vector2 pointerDownPosition;
    private Vector3 originalWorldPosition;
    private int originalRotationQuarterTurns;
    private Vector2Int originalDeploymentBottomLeftCell;
    private Vector3 dragOffset;
    private float pointerDepth;
    private bool isDragging;
    private bool wasDeployedAtPointerDown;
    private bool deploymentReleasedForDrag;

    /// <summary>
    /// Raised when the selected machine changes. Null means nothing is selected.
    /// </summary>
    public event Action<MiningMachineItem> SelectionChanged;

    private void Awake()
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (deploymentPrompt == null)
        {
            deploymentPrompt = GetComponentInChildren<TMP_Text>(true);
        }

        HideDeploymentPrompt();

        RegisterChildMachines();
    }

    private void Start()
    {
        SpawnInitialMachines();

        if (deploymentArea == null)
        {
            deploymentArea = FindFirstObjectByType<MiningMachineDeploymentArea>();
        }

        if (inputCamera == null)
        {
            Debug.LogWarning($"{name}：没有找到输入摄像机。请指定 Input Camera，或将摄像机 Tag 设为 MainCamera。", this);
        }

        if (machines.Count == 0)
        {
            Debug.LogWarning($"{name}：没有登记采矿机。请将机器设为子物体，或添加到 Machines 列表。", this);
        }
        else
        {
            Debug.Log($"{name}：待选区域已登记 {machines.Count} 台采矿机。", this);
        }

        if (deploymentArea == null)
        {
            Debug.LogWarning($"{name}：没有找到 MiningMachineDeploymentArea。请在开采区域网格上添加该组件。", this);
        }
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Vector2 pointerPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            HideDeploymentPrompt();
            BeginPointerInteraction(pointerPosition);

            // A selected machine can also be deployed by clicking a grid cell
            // directly. BeginPointerInteraction leaves pressedMachine null
            // when the pointer is not on another waiting machine, so this does
            // not interfere with the existing drag path.
            if (pressedMachine == null)
            {
                // A rotation SpriteObject is an action target, not a grid cell.
                // Do not interpret a click on it as click-to-deploy even if the
                // button happens to overlap the mining area on screen.
                if (!IsPointerOverRotationButton(pointerPosition))
                {
                    TryDeploySelectedByClick(pointerPosition);
                }
            }
        }

        if (pressedMachine != null && mouse.leftButton.isPressed)
        {
            UpdateDrag(pointerPosition);
        }

        if (pressedMachine != null && isDragging &&
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            pressedMachine.RotateClockwise();
            UpdateDrag(pointerPosition);
        }

        if (pressedMachine != null && mouse.leftButton.wasReleasedThisFrame)
        {
            EndPointerInteraction();
        }
    }

    /// <summary>
    /// Adds a machine to this waiting area. Can also be called when a machine is
    /// returned from the mining field.
    /// </summary>
    public void RegisterMachine(MiningMachineItem machine)
    {
        if (machine == null)
        {
            return;
        }

        if (!machines.Contains(machine))
        {
            machines.Add(machine);
        }

        machine.SetWaitingArea(this);
    }

    /// <summary>
    /// Instantiates the configured machine prefabs as children of this waiting
    /// area. Each instance is registered through the same path as a manually
    /// placed machine, so selection, dragging, deployment and reset work the
    /// same way.
    /// </summary>
    private void SpawnInitialMachines()
    {
        if (initialMachinesSpawned || !spawnInitialMachines)
        {
            return;
        }

        initialMachinesSpawned = true;
        int spawnIndex = 0;
        int columns = Mathf.Max(1, initialColumns);
        Transform parent = spawnParent != null ? spawnParent : transform;
        List<MiningMachineItem> spawnedMachines = new List<MiningMachineItem>();

        foreach (InitialMachineEntry entry in initialMachines)
        {
            if (entry == null || entry.Prefab == null)
            {
                continue;
            }

            for (int i = 0; i < entry.Count; i++)
            {
                MiningMachineItem machine = Instantiate(entry.Prefab, parent, false);
                int column = spawnIndex % columns;
                int row = spawnIndex / columns;
                machine.transform.localPosition = new Vector3(
                    initialStartLocalPosition.x + column * initialSpacing.x,
                    initialStartLocalPosition.y + row * initialSpacing.y,
                    machine.transform.localPosition.z);
                machine.name = $"{entry.Prefab.name}_{spawnIndex + 1}";
                RegisterMachine(machine);
                spawnedMachines.Add(machine);
                spawnIndex++;
            }
        }

        if (fitInitialMachinesToWaitingArea && spawnedMachines.Count > 0)
        {
            ArrangeInitialMachines(spawnedMachines, columns);
        }

        foreach (MiningMachineItem machine in spawnedMachines)
        {
            machine.CaptureWaitingAreaPlacement();
        }

        if (spawnIndex > 0)
        {
            Debug.Log($"{name}：开局生成 {spawnIndex} 台采矿机到待选区。", this);
        }
    }

    private void ArrangeInitialMachines(List<MiningMachineItem> spawnedMachines, int columns)
    {
        SpriteRenderer areaRenderer = GetComponent<SpriteRenderer>();
        if (areaRenderer == null || areaRenderer.sprite == null)
        {
            Debug.LogWarning($"{name}：自动排列初始采矿机需要待选区上的 SpriteRenderer。", this);
            return;
        }

        Bounds areaBounds = areaRenderer.bounds;
        float padding = Mathf.Max(0f, initialLayoutPadding);
        int count = spawnedMachines.Count;
        columns = Mathf.Clamp(columns, 1, count);
        int rows = Mathf.CeilToInt(count / (float)columns);

        List<Bounds> machineBounds = new List<Bounds>(count);
        float maxHalfWidth = 0f;
        float maxHalfHeight = 0f;

        foreach (MiningMachineItem machine in spawnedMachines)
        {
            if (!TryGetMachineVisualBounds(machine, out Bounds bounds))
            {
                bounds = new Bounds(machine.transform.position, Vector3.zero);
            }

            machineBounds.Add(bounds);
            maxHalfWidth = Mathf.Max(maxHalfWidth, bounds.extents.x);
            maxHalfHeight = Mathf.Max(maxHalfHeight, bounds.extents.y);
        }

        float minX = areaBounds.min.x + padding + maxHalfWidth;
        float maxX = areaBounds.max.x - padding - maxHalfWidth;
        float minY = areaBounds.min.y + padding + maxHalfHeight;
        float maxY = areaBounds.max.y - padding - maxHalfHeight;

        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float columnT = columns <= 1 ? 0.5f : column / (columns - 1f);
            float rowT = rows <= 1 ? 0.5f : row / (rows - 1f);
            Vector3 targetVisualCenter = new Vector3(
                Mathf.Lerp(minX, maxX, columnT),
                Mathf.Lerp(minY, maxY, rowT),
                spawnedMachines[i].transform.position.z);

            Vector3 visualOffset = machineBounds[i].center - spawnedMachines[i].transform.position;
            Vector3 targetRootPosition = targetVisualCenter - visualOffset;
            targetRootPosition.z = spawnedMachines[i].transform.position.z;
            spawnedMachines[i].transform.position = targetRootPosition;
        }
    }

    private bool TryGetMachineVisualBounds(MiningMachineItem machine, out Bounds bounds)
    {
        Renderer[] renderers = machine.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.bounds.size.sqrMagnitude <= 0f)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    /// <summary>
    /// Removes a machine from this waiting area and clears its selection.
    /// Call this when the machine is deployed to the mining field.
    /// </summary>
    public void RemoveMachine(MiningMachineItem machine)
    {
        if (machine == null)
        {
            return;
        }

        if (SelectedMachine == machine)
        {
            SetSelectedMachine(null);
        }

        machines.Remove(machine);
        machine.SetWaitingArea(null);
        machine.SetSelected(false);
    }

    /// <summary>
    /// Selects a waiting or deployed machine, or deselects it if it is already
    /// selected.
    /// </summary>
    public void ToggleSelection(MiningMachineItem machine)
    {
        bool isWaitingMachine = machine != null && machines.Contains(machine);
        bool isDeployedMachine = machine != null && deploymentArea != null &&
                                 deploymentArea.IsDeployed(machine);
        if (machine == null || (!isWaitingMachine && !isDeployedMachine))
        {
            return;
        }

        SetSelectedMachine(SelectedMachine == machine ? null : machine);
    }

    /// <summary>
    /// Clears the current selection. Useful after moving a machine.
    /// </summary>
    public void ClearSelection()
    {
        SetSelectedMachine(null);
    }

    /// <summary>
    /// Rotates the currently selected machine by one clockwise quarter-turn.
    /// Deployed machines are temporarily removed from the occupancy table so
    /// their own cells do not block the rotation check. If the rotated shape
    /// does not fit, both the visual rotation and the original deployment are
    /// restored.
    /// </summary>
    public bool RotateSelectedMachine()
    {
        MiningMachineItem machine = SelectedMachine;
        if (machine == null)
        {
            Debug.Log("没有选中的采矿机，无法旋转。", this);
            return false;
        }

        bool wasDeployed = deploymentArea != null &&
                           deploymentArea.IsDeployed(machine);
        Vector3 originalPosition = machine.transform.position;
        Quaternion originalRotation = machine.transform.rotation;
        int originalQuarterTurns = machine.RotationQuarterTurns;
        Vector2Int originalBottomLeftCell = machine.BottomLeftCell;

        if (wasDeployed)
        {
            deploymentArea.RemoveDeployment(machine);
        }

        // Waiting-area parents are often scaled non-uniformly to fit a colored
        // region. Rotate under a neutral parent so the Sprite/Tilemap does not
        // shear while the selected machine is turned by the button.
        Transform originalParent = machine.transform.parent;
        if (!wasDeployed && originalParent != null)
        {
            machine.transform.SetParent(null, true);
            machine.RotateClockwise();
            machine.transform.SetParent(originalParent, true);
        }
        else
        {
            machine.RotateClockwise();
        }

        if (!wasDeployed)
        {
            HideDeploymentPrompt();
            Debug.Log($"已旋转选中的采矿机：{machine.name}", machine);
            return true;
        }

        if (deploymentArea.TryGetDeploymentPreview(
                machine,
                originalPosition,
                out Vector3 previewPosition,
                out bool canDeploy,
                out string reason) &&
            canDeploy &&
            deploymentArea.TryDeploy(machine, previewPosition, out reason))
        {
            HideDeploymentPrompt();
            Debug.Log($"已旋转选中的采矿机：{machine.name}", machine);
            return true;
        }

        machine.transform.position = originalPosition;
        machine.transform.rotation = originalRotation;
        machine.RestoreRotationState(originalQuarterTurns);

        if (!deploymentArea.RestoreDeployment(
                machine,
                originalBottomLeftCell,
                out string restoreReason))
        {
            Debug.LogError($"旋转失败后恢复采矿机部署位置失败：{restoreReason}", machine);
        }

        string promptReason = string.IsNullOrEmpty(reason)
            ? blockedDeploymentMessage
            : reason;
        ShowDeploymentPrompt(promptReason);
        Debug.LogWarning($"采矿机旋转失败：{promptReason}", machine);
        return false;
    }

    private void RegisterChildMachines()
    {
        MiningMachineItem[] childMachines = GetComponentsInChildren<MiningMachineItem>(true);
        foreach (MiningMachineItem machine in childMachines)
        {
            RegisterMachine(machine);
        }
    }

    private void BeginPointerInteraction(Vector2 screenPosition)
    {
        if (inputCamera == null || pressedMachine != null)
        {
            return;
        }

        if (IsPointerOverRotationButton(screenPosition))
        {
            return;
        }

        MiningMachineItem hitMachine = FindMachineUnderPointer(screenPosition);
        if (hitMachine == null)
        {
            return;
        }

        pressedMachine = hitMachine;
        hitMachine.SetDeploymentPreview(false, false);
        pointerDownPosition = screenPosition;
        originalParent = hitMachine.transform.parent;
        originalLocalPosition = hitMachine.transform.localPosition;
        originalLocalRotation = hitMachine.transform.localRotation;
        originalLocalScale = hitMachine.transform.localScale;
        originalWorldPosition = hitMachine.transform.position;
        originalRotationQuarterTurns = hitMachine.RotationQuarterTurns;
        wasDeployedAtPointerDown = deploymentArea != null &&
            deploymentArea.IsDeployed(hitMachine);
        originalDeploymentBottomLeftCell = hitMachine.BottomLeftCell;

        // Waiting-area parents are often scaled differently on X and Y to fit
        // the colored region. Rotating a child under such a parent introduces
        // shear/stretching. Detach while dragging so rotation happens under a
        // neutral world parent, while keeping the same world transform.
        hitMachine.transform.SetParent(null, true);

        pointerDepth = inputCamera.WorldToScreenPoint(originalWorldPosition).z;
        dragOffset = originalWorldPosition - ScreenToWorld(screenPosition);
        isDragging = false;
        deploymentReleasedForDrag = false;
    }

    private void TryDeploySelectedByClick(Vector2 screenPosition)
    {
        if (SelectedMachine == null || deploymentArea == null || inputCamera == null)
        {
            return;
        }

        float depth = inputCamera.WorldToScreenPoint(
            SelectedMachine.transform.position).z;
        Vector3 pointerWorldPosition = inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));
        pointerWorldPosition.z = SelectedMachine.transform.position.z;

        // Ignore clicks outside the mining grid. A click inside the grid still
        // returns true when the location is occupied or out of bounds, allowing
        // us to report the actual deployment reason to the player.
        if (!deploymentArea.TryGetDeploymentPreview(
                SelectedMachine,
                pointerWorldPosition,
                out _,
                out bool canDeploy,
                out string reason))
        {
            return;
        }

        if (!canDeploy)
        {
            ShowDeploymentPrompt(reason);
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.LogWarning($"采矿机点击部署失败：{reason}", SelectedMachine);
            }

            return;
        }

        if (!deploymentArea.TryDeploy(SelectedMachine, pointerWorldPosition, out reason) &&
            !string.IsNullOrEmpty(reason))
        {
            ShowDeploymentPrompt(reason);
            Debug.LogWarning($"采矿机点击部署失败：{reason}", SelectedMachine);
        }
        else
        {
            HideDeploymentPrompt();
        }
    }

    private bool IsPointerOverRotationButton(Vector2 screenPosition)
    {
        if (inputCamera == null)
        {
            return false;
        }

        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null &&
                hit.collider.GetComponentInParent<RotateSelectedMachineClick>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        if (!isDragging)
        {
            float thresholdSquared = dragThresholdPixels * dragThresholdPixels;
            if ((screenPosition - pointerDownPosition).sqrMagnitude < thresholdSquared)
            {
                return;
            }

            isDragging = true;

            if (wasDeployedAtPointerDown && !deploymentReleasedForDrag &&
                deploymentArea != null)
            {
                deploymentReleasedForDrag = deploymentArea.RemoveDeployment(pressedMachine);
            }

            if (SelectedMachine != pressedMachine)
            {
                SetSelectedMachine(pressedMachine);
            }
        }

        Vector3 pointerWorldPosition = ScreenToWorld(screenPosition) + dragOffset;
        pointerWorldPosition.z = originalWorldPosition.z;

        if (deploymentArea != null && deploymentArea.TryGetDeploymentPreview(
                pressedMachine,
                pointerWorldPosition,
                out Vector3 previewPosition,
                out bool canDeploy,
                out string reason))
        {
            pressedMachine.transform.position = previewPosition;
            pressedMachine.SetDeploymentPreview(true, canDeploy);
            if (canDeploy)
            {
                HideDeploymentPrompt();
            }
            else
            {
                ShowDeploymentPrompt(reason);
            }
        }
        else
        {
            pressedMachine.transform.position = pointerWorldPosition;
            pressedMachine.SetDeploymentPreview(true, false);
            HideDeploymentPrompt();
        }
    }

    private void EndPointerInteraction()
    {
        MiningMachineItem machine = pressedMachine;

        if (isDragging)
        {
            if (wasDeployedAtPointerDown)
            {
                FinishDeployedMachineDrag(machine);
            }
            else
            {
                FinishWaitingMachineDrag(machine);
            }

            machine.SetDeploymentPreview(false, false);
        }
        else
        {
            ToggleSelection(machine);
        }

        pressedMachine = null;
        originalParent = null;
        isDragging = false;
        wasDeployedAtPointerDown = false;
        deploymentReleasedForDrag = false;
    }

    private void FinishWaitingMachineDrag(MiningMachineItem machine)
    {
        string reason = string.Empty;
        if (deploymentArea != null &&
            deploymentArea.TryDeploy(machine, machine.transform.position, out reason))
        {
            // TryDeploy removes the machine from this waiting area and clears selection.
            HideDeploymentPrompt();
            return;
        }

        RestoreWaitingTransform(machine);

        if (deploymentArea == null)
        {
            Debug.LogWarning("无法部署采矿机：没有找到 MiningMachineDeploymentArea。", machine);
        }
        else if (!string.IsNullOrEmpty(reason))
        {
            ShowDeploymentPrompt(reason);
            Debug.LogWarning($"采矿机部署失败：{reason}", machine);
        }
    }

    private void FinishDeployedMachineDrag(MiningMachineItem machine)
    {
        // Dropping over the waiting area's sprite returns the machine to its
        // original waiting slot and releases its grid cells.
        if (IsWorldPointInsideWaitingArea(machine.transform.position) &&
            machine.ReturnToWaitingArea())
        {
            if (SelectedMachine == machine)
            {
                SetSelectedMachine(null);
            }

            Debug.Log($"采矿机 {machine.name} 已返回待部署区。", machine);
            HideDeploymentPrompt();
            return;
        }

        string reason = string.Empty;
        if (deploymentArea != null &&
            deploymentArea.TryDeploy(machine, machine.transform.position, out reason))
        {
            if (SelectedMachine == machine)
            {
                SetSelectedMachine(null);
            }

            HideDeploymentPrompt();
            return;
        }

        RestoreDeployedMachine(machine, reason);
    }

    private void RestoreWaitingTransform(MiningMachineItem machine)
    {
        machine.transform.SetParent(originalParent, false);
        machine.transform.localPosition = originalLocalPosition;
        machine.transform.localRotation = originalLocalRotation;
        machine.transform.localScale = originalLocalScale;
        machine.RestoreRotationState(originalRotationQuarterTurns);
    }

    private void RestoreDeployedMachine(MiningMachineItem machine, string reason)
    {
        RestoreWaitingTransform(machine);

        if (deploymentReleasedForDrag && deploymentArea != null &&
            !deploymentArea.RestoreDeployment(
                machine,
                originalDeploymentBottomLeftCell,
                out string restoreReason))
        {
            Debug.LogError($"采矿机原部署位置恢复失败：{restoreReason}", machine);
        }

        if (SelectedMachine == machine)
        {
            SetSelectedMachine(null);
        }

        if (!string.IsNullOrEmpty(reason))
        {
            ShowDeploymentPrompt(reason);
            Debug.LogWarning($"采矿机移动失败：{reason}，已恢复原位置。", machine);
        }
    }

    private bool IsWorldPointInsideWaitingArea(Vector3 worldPosition)
    {
        SpriteRenderer areaRenderer = GetComponent<SpriteRenderer>();
        if (areaRenderer != null && areaRenderer.sprite != null)
        {
            Bounds bounds = areaRenderer.bounds;
            return worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x &&
                   worldPosition.y >= bounds.min.y && worldPosition.y <= bounds.max.y;
        }

        Collider2D areaCollider = GetComponent<Collider2D>();
        return areaCollider != null && areaCollider.OverlapPoint(worldPosition);
    }

    private void ShowDeploymentPrompt(string reason)
    {
        if (deploymentPrompt == null || string.IsNullOrEmpty(reason))
        {
            return;
        }

        deploymentPrompt.text = blockedDeploymentMessage;
        deploymentPrompt.gameObject.SetActive(true);
    }

    private void HideDeploymentPrompt()
    {
        if (deploymentPrompt != null)
        {
            deploymentPrompt.gameObject.SetActive(false);
        }
    }

    private MiningMachineItem FindMachineUnderPointer(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, machineLayerMask);

        MiningMachineItem nearestMachine = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            MiningMachineItem machine = hit.collider.GetComponentInParent<MiningMachineItem>();
            bool isWaitingMachine = machine != null && machines.Contains(machine);
            bool isDeployedMachine = machine != null && deploymentArea != null &&
                deploymentArea.IsDeployed(machine);
            if (machine != null && (isWaitingMachine || isDeployedMachine) &&
                hit.distance < nearestDistance)
            {
                nearestMachine = machine;
                nearestDistance = hit.distance;
            }
        }

        return nearestMachine;
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        return inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, pointerDepth));
    }

    private void SetSelectedMachine(MiningMachineItem machine)
    {
        if (SelectedMachine == machine)
        {
            return;
        }

        if (SelectedMachine != null)
        {
            Debug.Log($"取消选择采矿机：{SelectedMachine.name}", SelectedMachine);
            SelectedMachine.SetSelected(false);
        }

        SelectedMachine = machine;

        if (SelectedMachine != null)
        {
            SelectedMachine.SetSelected(true);
            Debug.Log($"选中采矿机：{SelectedMachine.name}", SelectedMachine);
        }

        SelectionChanged?.Invoke(SelectedMachine);
    }
}
