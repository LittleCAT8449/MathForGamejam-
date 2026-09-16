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
    [SerializeField] private MiningMachineWaitingArea waitingArea;
    [SerializeField] private Vector2Int footprintSize = Vector2Int.one;
    [Tooltip("启用后自动使用子物体 CustomTilemapShape 的 Shape Size 作为占地范围。")]
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

    public MiningMachineWaitingArea WaitingArea => waitingArea;
    public bool IsSelected { get; private set; }
    public bool IsPowered { get; private set; }
    public Vector2Int FootprintSize
    {
        get
        {
            if (autoFootprintFromShape)
            {
                CustomTilemapShape shape = GetComponentInChildren<CustomTilemapShape>(true);
                if (shape != null && shape.ShapeData != null)
                {
                    Vector2Int shapeSize = shape.ShapeData.ShapeSize;
                    return new Vector2Int(Mathf.Max(1, shapeSize.x), Mathf.Max(1, shapeSize.y));
                }
            }

            return new Vector2Int(
                Mathf.Max(1, footprintSize.x),
                Mathf.Max(1, footprintSize.y));
        }
    }
    public int ProductionNumber => Mathf.Max(0, productionNumber);
    public bool HasTilemapShape => GetComponentInChildren<CustomTilemapShape>(true) != null;
    public MiningMachineDeploymentArea DeploymentArea { get; private set; }
    public Vector2Int BottomLeftCell { get; private set; }

    internal void SetWaitingArea(MiningMachineWaitingArea waitingArea)
    {
        this.waitingArea = waitingArea;

        if (waitingArea != null && !hasHomePlacement)
        {
            homeWaitingArea = waitingArea;
            homeParent = transform.parent;
            homeLocalPosition = transform.localPosition;
            homeLocalRotation = transform.localRotation;
            homeLocalScale = transform.localScale;
            hasHomePlacement = true;
        }
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
    /// Updates the machine's selected highlight.
    /// </summary>
    public void SetSelected(bool selected)
    {
        CacheVisuals();
        IsSelected = selected;

        RefreshVisuals();
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
                spriteRenderers[i].color = IsSelected
                    ? selectedColor
                    : IsPowered ? poweredColor : originalColors[i];
            }
        }

        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] != null)
            {
                tilemaps[i].color = IsSelected
                    ? selectedColor
                    : IsPowered ? poweredColor : originalTilemapColors[i];
            }
        }
    }

    private void Awake()
    {
        CacheVisuals();

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
