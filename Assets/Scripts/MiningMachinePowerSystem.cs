using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connects off-grid batteries to deployed machines by collider contact, then
/// propagates power through edge-adjacent occupied cells on the grid.
/// </summary>
[RequireComponent(typeof(GridSystem), typeof(MiningMachineDeploymentArea))]
public class MiningMachinePowerSystem : MonoBehaviour
{
    [SerializeField] private GridSystem grid;
    [SerializeField] private MiningMachineDeploymentArea deploymentArea;
    [SerializeField] private List<BatteryPowerSource> batterySources = new List<BatteryPowerSource>();
    [Tooltip("电池与采矿机碰撞体之间允许的最大间隙（世界坐标单位）。")]
    [SerializeField, Min(0f)] private float batteryContactTolerance = 0.05f;

    private Coroutine deferredRecalculation;

    private void Awake()
    {
        CacheComponents();
        DiscoverBatterySources();
    }

    private void OnEnable()
    {
        CacheComponents();

        if (deploymentArea != null)
        {
            deploymentArea.MachinesChanged += HandlePowerTopologyChanged;
        }

        foreach (BatteryPowerSource battery in batterySources)
        {
            if (battery != null)
            {
                battery.StateChanged += HandlePowerTopologyChanged;
            }
        }
    }

    private void Start()
    {
        if (batterySources.Count == 0)
        {
            Debug.LogWarning($"{name}：没有找到 BatteryPowerSource，请给场景中的电池物体添加该组件。", this);
        }

        RecalculatePower();
    }

    private void OnDisable()
    {
        if (deploymentArea != null)
        {
            deploymentArea.MachinesChanged -= HandlePowerTopologyChanged;
        }

        foreach (BatteryPowerSource battery in batterySources)
        {
            if (battery != null)
            {
                battery.StateChanged -= HandlePowerTopologyChanged;
            }
        }

        if (deferredRecalculation != null)
        {
            StopCoroutine(deferredRecalculation);
            deferredRecalculation = null;
        }
    }

    /// <summary>
    /// Rebuilds power from active batteries through touching machine footprints.
    /// </summary>
    public void RecalculatePower()
    {
        if (deploymentArea == null)
        {
            return;
        }

        // Deployments move and reparent colliders immediately before this
        // event is raised. Sync the 2D physics world so battery contact uses
        // the machine's new bounds in the same frame.
        Physics2D.SyncTransforms();

        List<MiningMachineItem> machines = new List<MiningMachineItem>();
        foreach (MiningMachineItem machine in deploymentArea.DeployedMachines)
        {
            if (machine != null)
            {
                machines.Add(machine);
                machine.SetPowered(false);
            }
        }

        HashSet<MiningMachineItem> poweredMachines = new HashSet<MiningMachineItem>();
        Queue<MiningMachineItem> pending = new Queue<MiningMachineItem>();

        foreach (MiningMachineItem machine in machines)
        {
            if (TouchesActiveBattery(machine))
            {
                poweredMachines.Add(machine);
                pending.Enqueue(machine);
            }
        }

        while (pending.Count > 0)
        {
            MiningMachineItem poweredMachine = pending.Dequeue();
            foreach (MiningMachineItem candidate in machines)
            {
                if (!poweredMachines.Contains(candidate) && AreGridAdjacent(poweredMachine, candidate))
                {
                    poweredMachines.Add(candidate);
                    pending.Enqueue(candidate);
                }
            }
        }

        foreach (MiningMachineItem machine in poweredMachines)
        {
            machine.SetPowered(true);
        }

        Debug.Log($"电力更新：{poweredMachines.Count}/{machines.Count} 台采矿机已通电。", this);
    }

    private void HandlePowerTopologyChanged()
    {
        RecalculatePower();

        // A TilemapCollider2D can rebuild one physics step after a machine is
        // moved. Recheck once after that rebuild so collider-based contact is
        // also reliable for the first frame after deployment.
        if (deferredRecalculation == null && isActiveAndEnabled)
        {
            deferredRecalculation = StartCoroutine(RecalculatePowerAfterPhysics());
        }
    }

    private IEnumerator RecalculatePowerAfterPhysics()
    {
        yield return new WaitForFixedUpdate();
        deferredRecalculation = null;
        if (isActiveAndEnabled)
        {
            RecalculatePower();
        }
    }

    private bool TouchesActiveBattery(MiningMachineItem machine)
    {
        foreach (BatteryPowerSource battery in batterySources)
        {
            if (battery == null || !battery.IsBatteryActive || battery.SourceCollider == null)
            {
                continue;
            }

            Bounds batteryBounds = battery.SourceCollider.bounds;

            // A battery is outside the mining grid, so test each actual
            // occupied cell against it. This remains reliable before Unity
            // has rebuilt a TilemapCollider2D after deployment.
            if (TouchesBatteryByOccupiedCells(machine, batteryBounds))
            {
                return true;
            }

            Collider2D[] machineColliders = machine.GetComponentsInChildren<Collider2D>(true);

            foreach (Collider2D machineCollider in machineColliders)
            {
                if (machineCollider != null &&
                    BoundsAreAdjacent(
                        machineCollider.bounds,
                        batteryBounds,
                        batteryContactTolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool AreGridAdjacent(MiningMachineItem first, MiningMachineItem second)
    {
        List<Vector2Int> firstOffsets = new List<Vector2Int>();
        List<Vector2Int> secondOffsets = new List<Vector2Int>();
        first.GetOccupiedCellOffsets(firstOffsets);
        second.GetOccupiedCellOffsets(secondOffsets);

        HashSet<Vector2Int> secondCells = new HashSet<Vector2Int>();
        foreach (Vector2Int offset in secondOffsets)
        {
            secondCells.Add(second.BottomLeftCell + offset);
        }

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        foreach (Vector2Int offset in firstOffsets)
        {
            Vector2Int firstCell = first.BottomLeftCell + offset;
            foreach (Vector2Int direction in directions)
            {
                if (secondCells.Contains(firstCell + direction))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TouchesBatteryByOccupiedCells(MiningMachineItem machine, Bounds batteryBounds)
    {
        if (grid == null || machine.DeploymentArea != deploymentArea)
        {
            return false;
        }

        List<Vector2Int> offsets = new List<Vector2Int>();
        machine.GetOccupiedCellOffsets(offsets);
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = machine.BottomLeftCell + offset;
            if (grid.TryGetCellWorldBounds(cell, out Bounds cellBounds) &&
                BoundsAreAdjacent(cellBounds, batteryBounds, batteryContactTolerance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BoundsAreAdjacent(Bounds first, Bounds second, float tolerance)
    {
        // Power connections are 2D; ignore sorting depth differences.
        float xGap = Mathf.Max(first.min.x - second.max.x, second.min.x - first.max.x);
        float yGap = Mathf.Max(first.min.y - second.max.y, second.min.y - first.max.y);
        return xGap <= tolerance && yGap <= tolerance;
    }

    private void CacheComponents()
    {
        if (grid == null)
        {
            grid = GetComponent<GridSystem>();
        }

        if (deploymentArea == null)
        {
            deploymentArea = GetComponent<MiningMachineDeploymentArea>();
        }
    }

    private void DiscoverBatterySources()
    {
        BatteryPowerSource[] sceneBatteries = FindObjectsByType<BatteryPowerSource>(FindObjectsSortMode.None);
        foreach (BatteryPowerSource battery in sceneBatteries)
        {
            if (battery != null && !batterySources.Contains(battery))
            {
                batterySources.Add(battery);
            }
        }
    }
}
