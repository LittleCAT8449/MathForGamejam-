using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Attach this component to the mining-area grid object. It validates dropped
/// machines, snaps them to the grid, and prevents occupied filled cells from
/// being reused.
/// </summary>
[RequireComponent(typeof(GridSystem))]
public class MiningMachineDeploymentArea : MonoBehaviour
{
    [SerializeField] private GridSystem grid;
    [FormerlySerializedAs("deployedMachineScale")]
    [SerializeField] private Vector2 cellScale = new Vector2(0.1f, 0.1f);

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private readonly Dictionary<MiningMachineItem, List<Vector2Int>> placements =
        new Dictionary<MiningMachineItem, List<Vector2Int>>();

    public IEnumerable<MiningMachineItem> DeployedMachines => placements.Keys;
    public event Action MachinesChanged;

    /// <summary>
    /// Calculates the position a machine would occupy without changing any
    /// deployment state. The returned position is snapped to the grid whenever
    /// the pointer is inside the mining area, even when the placement is
    /// invalid because of an occupied cell or an out-of-bounds footprint.
    /// </summary>
    public bool TryGetDeploymentPreview(
        MiningMachineItem machine,
        Vector3 pointerWorldPosition,
        out Vector3 previewPosition,
        out bool canDeploy,
        out string reason)
    {
        previewPosition = pointerWorldPosition;
        canDeploy = false;
        reason = string.Empty;

        if (machine == null)
        {
            reason = "采矿机不存在。";
            return false;
        }

        if (grid == null)
        {
            reason = "开采区域没有找到 GridSystem。";
            return false;
        }

        if (!grid.TryWorldToCell(pointerWorldPosition, out Vector2Int centerCell))
        {
            reason = "请把采矿机放在开采网格内。";
            return false;
        }

        Vector2Int footprint = machine.FootprintSize;
        Vector2Int bottomLeftCell = centerCell -
            new Vector2Int(footprint.x / 2, footprint.y / 2);

        if (!grid.IsFootprintInsideGrid(bottomLeftCell, footprint))
        {
            reason = "采矿机超出开采区域边界。";
        }
        else if (!grid.TryGetFootprintCenterWorld(
                     bottomLeftCell,
                     footprint,
                     out previewPosition))
        {
            reason = "无法计算采矿机的网格位置。";
        }
        else
        {
            List<Vector2Int> cells = GetOccupiedCells(machine, bottomLeftCell);
            foreach (Vector2Int cell in cells)
            {
                if (occupiedCells.Contains(cell))
                {
                    reason = "目标格子已被占用，无法部署。";
                    break;
                }
            }
        }

        canDeploy = string.IsNullOrEmpty(reason);
        return true;
    }

    private void Awake()
    {
        CacheGrid();
    }

    private void OnValidate()
    {
        cellScale.x = Mathf.Max(0.001f, cellScale.x);
        cellScale.y = Mathf.Max(0.001f, cellScale.y);
        CacheGrid();
    }

    /// <summary>
    /// Attempts to deploy a machine at its current world position.
    /// </summary>
    public bool TryDeploy(MiningMachineItem machine, Vector3 droppedWorldPosition, out string reason)
    {
        reason = string.Empty;

        if (machine == null)
        {
            reason = "采矿机不存在。";
            return false;
        }

        if (placements.ContainsKey(machine))
        {
            reason = "这台采矿机已经部署。";
            return false;
        }

        if (!TryGetDeploymentPreview(
                machine,
                droppedWorldPosition,
                out Vector3 snappedPosition,
                out bool canDeploy,
                out reason))
        {
            return false;
        }

        if (!canDeploy)
        {
            return false;
        }

        grid.TryWorldToCell(droppedWorldPosition, out Vector2Int centerCell);
        Vector2Int footprint = machine.FootprintSize;
        // The machine pivot is treated as the footprint center. Even-sized
        // footprints are biased toward the lower-left cell.
        Vector2Int bottomLeftCell = centerCell - new Vector2Int(footprint.x / 2, footprint.y / 2);

        List<Vector2Int> cells = GetOccupiedCells(machine, bottomLeftCell);

        if (machine.WaitingArea != null)
        {
            machine.WaitingArea.RemoveMachine(machine);
        }

        machine.transform.SetParent(grid.transform, true);
        machine.transform.position = snappedPosition;
        Vector3 localScale = machine.transform.localScale;
        // A Tilemap machine already contains one sprite per footprint cell, so
        // its root scale represents the size of one cell. A single Sprite
        // machine represents the whole footprint and needs the multiplication.
        if (machine.HasTilemapShape)
        {
            localScale.x = cellScale.x;
            localScale.y = cellScale.y;
        }
        else
        {
            localScale.x = cellScale.x * footprint.x;
            localScale.y = cellScale.y * footprint.y;
        }
        machine.transform.localScale = localScale;
        machine.SetDeploymentLocation(this, bottomLeftCell);

        placements.Add(machine, cells);
        foreach (Vector2Int cell in cells)
        {
            occupiedCells.Add(cell);
        }

        Debug.Log($"采矿机 {machine.name} 已部署到格子 {bottomLeftCell}。", machine);
        MachinesChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Releases a deployed machine's occupied cells. The caller can then return
    /// the machine to the waiting area.
    /// </summary>
    public bool RemoveDeployment(MiningMachineItem machine)
    {
        if (machine == null || !placements.TryGetValue(machine, out List<Vector2Int> cells))
        {
            return false;
        }

        foreach (Vector2Int cell in cells)
        {
            occupiedCells.Remove(cell);
        }

        placements.Remove(machine);
        machine.SetDeploymentLocation(null, default);
        MachinesChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Frees every occupied cell and restores deployed machines to their
    /// original waiting-area positions.
    /// </summary>
    public void ReturnAllDeployedMachines()
    {
        List<MiningMachineItem> deployed = new List<MiningMachineItem>(placements.Keys);
        foreach (MiningMachineItem machine in deployed)
        {
            if (machine == null)
            {
                continue;
            }

            RemoveDeployment(machine);
            if (!machine.ReturnToWaitingArea())
            {
                Debug.LogWarning($"采矿机 {machine.name} 没有记录待选区位置，无法自动返回。", machine);
            }
        }
    }

    private List<Vector2Int> GetOccupiedCells(
        MiningMachineItem machine,
        Vector2Int bottomLeftCell)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();
        machine.GetOccupiedCellOffsets(offsets);

        List<Vector2Int> cells = new List<Vector2Int>(offsets.Count);
        foreach (Vector2Int offset in offsets)
        {
            cells.Add(bottomLeftCell + offset);
        }

        return cells;
    }

    private void CacheGrid()
    {
        if (grid == null)
        {
            grid = GetComponent<GridSystem>();
        }
    }
}
