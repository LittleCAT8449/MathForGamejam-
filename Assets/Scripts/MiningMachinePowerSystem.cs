using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connects off-grid batteries to deployed machines by collider contact, then
/// propagates power through edge-adjacent machine footprints on the grid.
/// </summary>
[RequireComponent(typeof(GridSystem), typeof(MiningMachineDeploymentArea))]
public class MiningMachinePowerSystem : MonoBehaviour
{
    [SerializeField] private GridSystem grid;
    [SerializeField] private MiningMachineDeploymentArea deploymentArea;
    [SerializeField] private List<BatteryPowerSource> batterySources = new List<BatteryPowerSource>();
    [SerializeField, Min(0f)] private float batteryContactTolerance = 0.02f;

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
            deploymentArea.MachinesChanged += RecalculatePower;
        }

        foreach (BatteryPowerSource battery in batterySources)
        {
            if (battery != null)
            {
                battery.StateChanged += RecalculatePower;
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
            deploymentArea.MachinesChanged -= RecalculatePower;
        }

        foreach (BatteryPowerSource battery in batterySources)
        {
            if (battery != null)
            {
                battery.StateChanged -= RecalculatePower;
            }
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

    private bool TouchesActiveBattery(MiningMachineItem machine)
    {
        Collider2D machineCollider = machine.GetComponent<Collider2D>();
        if (machineCollider == null)
        {
            return false;
        }

        foreach (BatteryPowerSource battery in batterySources)
        {
            if (battery == null || !battery.IsBatteryActive || battery.SourceCollider == null)
            {
                continue;
            }

            if (BoundsAreAdjacent(machineCollider.bounds, battery.SourceCollider.bounds, batteryContactTolerance))
            {
                return true;
            }
        }

        return false;
    }

    private bool AreGridAdjacent(MiningMachineItem first, MiningMachineItem second)
    {
        Vector2Int firstMin = first.BottomLeftCell;
        Vector2Int firstMax = firstMin + first.FootprintSize;
        Vector2Int secondMin = second.BottomLeftCell;
        Vector2Int secondMax = secondMin + second.FootprintSize;

        bool verticalOverlap = firstMin.y < secondMax.y && secondMin.y < firstMax.y;
        bool horizontalOverlap = firstMin.x < secondMax.x && secondMin.x < firstMax.x;
        bool touchesHorizontally = firstMax.x == secondMin.x || secondMax.x == firstMin.x;
        bool touchesVertically = firstMax.y == secondMin.y || secondMax.y == firstMin.y;

        return (touchesHorizontally && verticalOverlap) ||
               (touchesVertically && horizontalOverlap);
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
