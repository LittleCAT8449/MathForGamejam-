using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates a custom-shaped Tilemap from cells selected in the Inspector.
/// Assign a RuleTile to make the filled cells connect automatically.
/// </summary>
public class CustomTilemapShape : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase tile;
    [SerializeField] private TilemapShapeData shapeData;
    [SerializeField] private Vector2Int tilemapOrigin;
    [Tooltip("将形状的占地范围中心对齐到采矿机根物体的 Pivot。启用后，L 形等不规则形状也会按完整 Shape Size 居中到网格。")]
    [SerializeField] private bool centerOnMachinePivot = true;
    [SerializeField] private bool generateOnAwake = true;

    public TilemapShapeData ShapeData => shapeData;

    private void Awake()
    {
        if (generateOnAwake)
        {
            Generate();
        }
    }

    private void OnValidate()
    {
        if (centerOnMachinePivot && tilemap != null && shapeData != null)
        {
            CenterTilemapOnShape();
        }
    }

    /// <summary>
    /// Rebuilds the Tilemap from the cells selected in the Inspector.
    /// </summary>
    [ContextMenu("Generate Shape")]
    public void Generate()
    {
        if (tilemap == null)
        {
            tilemap = GetComponentInChildren<Tilemap>();
        }

        if (tilemap == null)
        {
            Debug.LogWarning($"{name}：没有找到 Tilemap。", this);
            return;
        }

        tilemap.ClearAllTiles();

        if (tile == null)
        {
            Debug.LogWarning($"{name}：没有指定 Tile 或 RuleTile。", this);
            return;
        }

        if (shapeData == null)
        {
            Debug.LogWarning($"{name}：没有指定 Tilemap Shape Data。", this);
            return;
        }

        foreach (Vector2Int cell in shapeData.FilledCells)
        {
            tilemap.SetTile(
                new Vector3Int(tilemapOrigin.x + cell.x, tilemapOrigin.y + cell.y, 0),
                tile);
        }

        tilemap.RefreshAllTiles();

        if (centerOnMachinePivot)
        {
            CenterTilemapOnShape();
        }
    }

    /// <summary>
    /// The deployment system puts the machine root at the center of its
    /// footprint. Tilemap cells are authored from (0, 0), so move the Tilemap
    /// by half of the actually filled bounds to make that same center the pivot.
    /// </summary>
    private void CenterTilemapOnShape()
    {
        if (tilemap == null || shapeData == null)
        {
            return;
        }

        Grid grid = tilemap.GetComponentInParent<Grid>();
        Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
        Vector2Int minimum = Vector2Int.zero;
        Vector2Int size = shapeData.ShapeSize;
        shapeData.TryGetFilledBounds(out minimum, out size);
        Vector3 shapeCenterInCells = new Vector3(
            tilemapOrigin.x + minimum.x + size.x * 0.5f,
            tilemapOrigin.y + minimum.y + size.y * 0.5f,
            0f);

        Vector3 localPosition = tilemap.transform.localPosition;
        localPosition.x = -shapeCenterInCells.x * cellSize.x;
        localPosition.y = -shapeCenterInCells.y * cellSize.y;
        tilemap.transform.localPosition = localPosition;
    }

    public void ClearShape()
    {
        if (tilemap == null)
        {
            tilemap = GetComponentInChildren<Tilemap>();
        }

        if (tilemap != null)
        {
            tilemap.ClearAllTiles();
        }
    }

}
