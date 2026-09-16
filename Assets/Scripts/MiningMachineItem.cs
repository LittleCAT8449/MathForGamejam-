using UnityEngine;

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
    [SerializeField, Min(0)] private int productionPerCell = 1;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private bool visualsCached;

    public MiningMachineWaitingArea WaitingArea => waitingArea;
    public bool IsSelected { get; private set; }
    public bool IsPowered { get; private set; }
    public Vector2Int FootprintSize => new Vector2Int(
        Mathf.Max(1, footprintSize.x),
        Mathf.Max(1, footprintSize.y));
    public int ProductionNumber => FootprintSize.x * FootprintSize.y * productionPerCell;
    public MiningMachineDeploymentArea DeploymentArea { get; private set; }
    public Vector2Int BottomLeftCell { get; private set; }

    internal void SetWaitingArea(MiningMachineWaitingArea waitingArea)
    {
        this.waitingArea = waitingArea;
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
        productionPerCell = Mathf.Max(0, productionPerCell);
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

        visualsCached = true;
    }
}
