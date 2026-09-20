using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Add this component to each machine object inside a MiningMachineWaitingArea.
/// A Collider2D is required so the waiting area can detect clicks on the machine.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MiningMachineItem : MonoBehaviour
{
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color poweredColor = new Color(0.45f, 1f, 0.45f, 1f);
    [Tooltip("拖拽到可用网格位置时的预览颜色。")]
    [SerializeField] private Color previewValidColor = new Color(0.25f, 1f, 0.35f, 0.65f);
    [Tooltip("拖拽到无效位置时的预览颜色。")]
    [SerializeField] private Color previewInvalidColor = new Color(1f, 0.2f, 0.2f, 0.65f);
    [SerializeField] private MiningMachineWaitingArea waitingArea;
    [SerializeField] private Vector2Int footprintSize = Vector2Int.one;
    [Tooltip("启用后自动使用子物体 CustomTilemapShape 的实际填充格外接范围作为占地范围。")]
    [SerializeField] private bool autoFootprintFromShape = true;
    [Tooltip("这台机器开始采矿时生成的数字。不同机器可以设置不同值。")]
    [SerializeField, Min(0)] private int productionNumber = 1;

    private MiningMachineWaitingArea homeWaitingArea;
    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 homeLocalScale;
    private bool hasHomePlacement;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Tilemap[] tilemaps;
    private Color[] originalTilemapColors;
    private bool visualsCached;
    private bool isDeploymentPreview;
    private bool deploymentPreviewValid;
    private Vector3 prefabLocalScale;
    private bool prefabLocalScaleCaptured;

    public MiningMachineWaitingArea WaitingArea => waitingArea;
    public bool IsSelected { get; private set; }
    public bool IsPowered { get; private set; }
    /// <summary>
    /// Number of clockwise quarter-turns applied while dragging this machine.
    /// </summary>
    public int RotationQuarterTurns { get; private set; }
    public Vector2Int FootprintSize
    {
        get
        {
            Vector2Int baseSize;
            if (autoFootprintFromShape)
            {
                CustomTilemapShape shape = GetComponentInChildren<CustomTilemapShape>(true);
                if (shape != null && shape.ShapeData != null)
                {
                    if (shape.ShapeData.TryGetFilledBounds(
                            out _,
                            out Vector2Int filledSize))
                    {
                        baseSize = new Vector2Int(
                            Mathf.Max(1, filledSize.x),
                            Mathf.Max(1, filledSize.y));
                        return GetRotatedSize(baseSize);
                    }

                    Vector2Int shapeSize = shape.ShapeData.ShapeSize;
                    baseSize = new Vector2Int(
                        Mathf.Max(1, shapeSize.x),
                        Mathf.Max(1, shapeSize.y));
                    return GetRotatedSize(baseSize);
                }
            }

            baseSize = new Vector2Int(
                Mathf.Max(1, footprintSize.x),
                Mathf.Max(1, footprintSize.y));
            return GetRotatedSize(baseSize);
        }
    }
    public int ProductionNumber => Mathf.Max(0, productionNumber);
    public bool HasTilemapShape => GetComponentInChildren<CustomTilemapShape>(true) != null;
    /// <summary>
    /// The scale authored on the prefab (or scene instance). The waiting area
    /// uses it as the baseline for display-size normalization so that repeated
    /// page refreshes cannot accumulate scaling error.
    /// </summary>
    public Vector3 PrefabLocalScale
    {
        get
        {
            CapturePrefabLocalScale();
            return prefabLocalScale;
        }
    }
    public MiningMachineDeploymentArea DeploymentArea { get; private set; }
    public Vector2Int BottomLeftCell { get; private set; }

    /// <summary>
    /// Records the prefab-authored scale once, before any display-size
    /// normalization is applied. Awake order is not guaranteed, so this is
    /// lazy and idempotent to keep the baseline stable.
    /// </summary>
    private void CapturePrefabLocalScale()
    {
        if (prefabLocalScaleCaptured)
        {
            return;
        }

        prefabLocalScale = transform.localScale;
        prefabLocalScaleCaptured = true;
    }

    /// <summary>
    /// Writes the cells that are physically occupied inside the machine's
    /// rectangular footprint. Tilemap shapes use only their filled cells, so
    /// empty cells inside an L or other irregular shape remain available.
    /// </summary>
    public void GetOccupiedCellOffsets(List<Vector2Int> offsets)
    {
        if (offsets == null)
        {
            return;
        }

        offsets.Clear();

        Vector2Int footprint = FootprintSize;

        if (autoFootprintFromShape)
        {
            CustomTilemapShape shape = GetComponentInChildren<CustomTilemapShape>(true);
            if (shape != null && shape.ShapeData != null &&
                shape.ShapeData.TryGetFilledBounds(
                    out Vector2Int filledMinimum,
                    out _))
            {
                foreach (Vector2Int cell in shape.ShapeData.FilledCells)
                {
                    offsets.Add(RotateCell(cell - filledMinimum, shape.ShapeData, RotationQuarterTurns));
                }

                if (offsets.Count > 0)
                {
                    return;
                }
            }
        }

        for (int x = 0; x < footprint.x; x++)
        {
            for (int y = 0; y < footprint.y; y++)
            {
                offsets.Add(new Vector2Int(x, y));
            }
        }
    }

    internal void SetWaitingArea(MiningMachineWaitingArea waitingArea)
    {
        this.waitingArea = waitingArea;

        if (waitingArea != null && !hasHomePlacement)
        {
            CaptureWaitingAreaPlacement();
        }
    }

    /// <summary>
    /// Saves the machine's current waiting-area transform. Runtime-spawned
    /// machines are positioned immediately after Instantiate, so the waiting
    /// area calls this once the initial layout has been applied.
    /// </summary>
    internal void CaptureWaitingAreaPlacement()
    {
        if (waitingArea == null)
        {
            return;
        }

        homeWaitingArea = waitingArea;
        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
        hasHomePlacement = true;
    }

    /// <summary>
    /// Restores this machine to the transform it had when first registered in
    /// its waiting area.
    /// </summary>
    public bool ReturnToWaitingArea()
    {
        if (homeWaitingArea == null || !hasHomePlacement)
        {
            return false;
        }

        transform.SetParent(homeParent != null ? homeParent : homeWaitingArea.transform, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        transform.localScale = homeLocalScale;
        RotationQuarterTurns = 0;

        SetDeploymentLocation(null, default);
        SetPowered(false);
        SetSelected(false);
        homeWaitingArea.RegisterMachine(this);
        return true;
    }

    internal void SetDeploymentLocation(MiningMachineDeploymentArea area, Vector2Int bottomLeftCell)
    {
        DeploymentArea = area;
        BottomLeftCell = bottomLeftCell;
    }

    /// <summary>
    /// Rotates the machine clockwise by 90 degrees. The root pivot stays in
    /// place; the deployment system recalculates occupied cells on the next
    /// preview/deploy check.
    /// </summary>
    public void RotateClockwise()
    {
        RotationQuarterTurns = (RotationQuarterTurns + 1) % 4;
        transform.Rotate(0f, 0f, -90f, Space.Self);
    }

    internal void RestoreRotationState(int quarterTurns)
    {
        RotationQuarterTurns = ((quarterTurns % 4) + 4) % 4;
    }

    private Vector2Int GetRotatedSize(Vector2Int size)
    {
        return RotationQuarterTurns % 2 == 1
            ? new Vector2Int(size.y, size.x)
            : size;
    }

    private static Vector2Int RotateCell(
        Vector2Int cell,
        TilemapShapeData shapeData,
        int quarterTurns)
    {
        Vector2Int size = shapeData.TryGetFilledBounds(out _, out Vector2Int filledSize)
            ? filledSize
            : shapeData.ShapeSize;

        switch (quarterTurns % 4)
        {
            case 1:
                // The visual root rotates clockwise (-90 degrees on Z).
                // Rotate the authored cell coordinates in the same direction
                // so logical occupancy stays aligned with what is rendered.
                return new Vector2Int(cell.y, size.x - 1 - cell.x);
            case 2:
                return new Vector2Int(size.x - 1 - cell.x, size.y - 1 - cell.y);
            case 3:
                return new Vector2Int(size.y - 1 - cell.y, cell.x);
            default:
                return cell;
        }
    }

    /// <summary>
    /// Changes the machine's render color while it is being dragged over the
    /// mining area. The preview state does not affect deployment or power.
    /// </summary>
    internal void SetDeploymentPreview(bool visible, bool valid)
    {
        CacheVisuals();
        isDeploymentPreview = visible;
        deploymentPreviewValid = valid;
        RefreshVisuals();
    }

    /// <summary>
    /// Updates the machine's selected highlight.
    /// </summary>
    public void SetSelected(bool selected)
    {
        CacheVisuals();
        IsSelected = selected;

        RefreshVisuals();

        if (selected)
        {
            TutorialTooltipController.FindOrCreate()
                .OnMiningMachineSelected(this);
        }
    }

    public void SetPowered(bool powered)
    {
        CacheVisuals();
        IsPowered = powered;

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = isDeploymentPreview
                    ? deploymentPreviewValid ? previewValidColor : previewInvalidColor
                    : IsSelected
                    ? selectedColor
                    : IsPowered ? poweredColor : originalColors[i];
            }
        }

        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] != null)
            {
                tilemaps[i].color = isDeploymentPreview
                    ? deploymentPreviewValid ? previewValidColor : previewInvalidColor
                    : IsSelected
                    ? selectedColor
                    : IsPowered ? poweredColor : originalTilemapColors[i];
            }
        }
    }

    private void Awake()
    {
        CacheVisuals();

        // Record the prefab-authored scale before the waiting area applies any
        // display-size normalization, so repeated page refreshes always scale
        // from the same baseline instead of compounding.
        CapturePrefabLocalScale();

        if (waitingArea == null)
        {
            waitingArea = GetComponentInParent<MiningMachineWaitingArea>();
        }

        if (waitingArea != null)
        {
            waitingArea.RegisterMachine(this);
        }
    }

    private void OnValidate()
    {
        footprintSize.x = Mathf.Max(1, footprintSize.x);
        footprintSize.y = Mathf.Max(1, footprintSize.y);
        productionNumber = Mathf.Max(0, productionNumber);
    }

    private void CacheVisuals()
    {
        if (visualsCached)
        {
            return;
        }

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalColors[i] = spriteRenderers[i].color;
        }

        tilemaps = GetComponentsInChildren<Tilemap>(true);
        originalTilemapColors = new Color[tilemaps.Length];
        for (int i = 0; i < tilemaps.Length; i++)
        {
            originalTilemapColors[i] = tilemaps[i].color;
        }

        visualsCached = true;
    }
}
