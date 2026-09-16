using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("开局生成")]
    [SerializeField] private bool spawnInitialMachines = true;
    [SerializeField] private List<InitialMachineEntry> initialMachines =
        new List<InitialMachineEntry>();
    [Tooltip("生成实例的父物体。可指定原来放置 Square 的‘选择物体’；为空时使用待选区域自身。")]
    [SerializeField] private Transform spawnParent;
    [SerializeField] private Vector2 initialStartLocalPosition = Vector2.zero;
    [SerializeField] private Vector2 initialSpacing = new Vector2(1.2f, 1.2f);
    [SerializeField, Min(1)] private int initialColumns = 4;

    private bool initialMachinesSpawned;

    public MiningMachineItem SelectedMachine { get; private set; }

    private MiningMachineItem pressedMachine;
    private Transform originalParent;
    private Vector2 pointerDownPosition;
    private Vector3 originalWorldPosition;
    private Vector3 dragOffset;
    private float pointerDepth;
    private bool isDragging;

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
            BeginPointerInteraction(pointerPosition);
        }

        if (pressedMachine != null && mouse.leftButton.isPressed)
        {
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
                spawnIndex++;
            }
        }

        if (spawnIndex > 0)
        {
            Debug.Log($"{name}：开局生成 {spawnIndex} 台采矿机到待选区。", this);
        }
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
    /// Selects a machine, or deselects it if it is already selected.
    /// </summary>
    public void ToggleSelection(MiningMachineItem machine)
    {
        if (machine == null || !machines.Contains(machine))
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

        MiningMachineItem hitMachine = FindMachineUnderPointer(screenPosition);
        if (hitMachine == null)
        {
            return;
        }

        pressedMachine = hitMachine;
        pointerDownPosition = screenPosition;
        originalParent = hitMachine.transform.parent;
        originalWorldPosition = hitMachine.transform.position;
        pointerDepth = inputCamera.WorldToScreenPoint(originalWorldPosition).z;
        dragOffset = originalWorldPosition - ScreenToWorld(screenPosition);
        isDragging = false;
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

            if (SelectedMachine != pressedMachine)
            {
                SetSelectedMachine(pressedMachine);
            }
        }

        Vector3 position = ScreenToWorld(screenPosition) + dragOffset;
        position.z = originalWorldPosition.z;
        pressedMachine.transform.position = position;
    }

    private void EndPointerInteraction()
    {
        MiningMachineItem machine = pressedMachine;

        if (isDragging)
        {
            string reason = string.Empty;
            if (deploymentArea != null &&
                deploymentArea.TryDeploy(machine, machine.transform.position, out reason))
            {
                // TryDeploy removes the machine from this waiting area and clears selection.
            }
            else
            {
                machine.transform.SetParent(originalParent, true);
                machine.transform.position = originalWorldPosition;

                if (deploymentArea == null)
                {
                    Debug.LogWarning("无法部署采矿机：没有找到 MiningMachineDeploymentArea。", machine);
                }
                else if (!string.IsNullOrEmpty(reason))
                {
                    Debug.LogWarning($"采矿机部署失败：{reason}", machine);
                }
            }
        }
        else
        {
            ToggleSelection(machine);
        }

        pressedMachine = null;
        originalParent = null;
        isDragging = false;
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
            if (machine != null && machines.Contains(machine) && hit.distance < nearestDistance)
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
