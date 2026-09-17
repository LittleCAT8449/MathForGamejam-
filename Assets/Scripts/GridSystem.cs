using UnityEngine;

/// <summary>
/// Grid coordinates and cell positions based on the SpriteRenderer's sprite bounds.
/// Add this component to the SpriteRenderer object used as the grid background.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GridSystem : MonoBehaviour
{
    [SerializeField, Min(1)] private int columns = 5;
    [SerializeField, Min(1)] private int rows = 5;
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 1f, 0.8f);

    private SpriteRenderer spriteRenderer;

    public int Columns => columns;
    public int Rows => rows;

    private void Awake()
    {
        CacheSpriteRenderer();
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        CacheSpriteRenderer();
    }

    /// <summary>
    /// Converts a world position to a zero-based cell coordinate.
    /// Returns false when the position is outside the sprite bounds.
    /// </summary>
    public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
    {
        cell = default;

        if (!TryGetLocalSpriteBounds(out Bounds bounds))
        {
            return false;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float cellWidth = bounds.size.x / columns;
        float cellHeight = bounds.size.y / rows;

        if (cellWidth <= 0f || cellHeight <= 0f ||
            localPosition.x < bounds.min.x || localPosition.x >= bounds.max.x ||
            localPosition.y < bounds.min.y || localPosition.y >= bounds.max.y)
        {
            return false;
        }

        cell = new Vector2Int(
            Mathf.FloorToInt((localPosition.x - bounds.min.x) / cellWidth),
            Mathf.FloorToInt((localPosition.y - bounds.min.y) / cellHeight));

        return IsCellInsideGrid(cell);
    }

    /// <summary>
    /// Returns the world position at the center of a cell.
    /// </summary>
    public bool TryGetCellCenterWorld(Vector2Int cell, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!IsCellInsideGrid(cell) || !TryGetLocalSpriteBounds(out Bounds bounds))
        {
            return false;
        }

        float cellWidth = bounds.size.x / columns;
        float cellHeight = bounds.size.y / rows;
        Vector3 localCenter = new Vector3(
            bounds.min.x + (cell.x + 0.5f) * cellWidth,
            bounds.min.y + (cell.y + 0.5f) * cellHeight,
            bounds.center.z);

        worldPosition = transform.TransformPoint(localCenter);
        return true;
    }

    /// <summary>
    /// Returns the world-space bounds of one grid cell. This is useful for
    /// systems that need to test contact with a cell without relying on a
    /// TilemapCollider2D having rebuilt its geometry yet.
    /// </summary>
    public bool TryGetCellWorldBounds(Vector2Int cell, out Bounds worldBounds)
    {
        worldBounds = default;

        if (!IsCellInsideGrid(cell) || !TryGetLocalSpriteBounds(out Bounds bounds))
        {
            return false;
        }

        float cellWidth = bounds.size.x / columns;
        float cellHeight = bounds.size.y / rows;
        if (cellWidth <= 0f || cellHeight <= 0f)
        {
            return false;
        }

        float minX = bounds.min.x + cell.x * cellWidth;
        float minY = bounds.min.y + cell.y * cellHeight;
        float maxX = minX + cellWidth;
        float maxY = minY + cellHeight;

        Vector3 bottomLeft = transform.TransformPoint(new Vector3(minX, minY, bounds.center.z));
        Vector3 topLeft = transform.TransformPoint(new Vector3(minX, maxY, bounds.center.z));
        Vector3 bottomRight = transform.TransformPoint(new Vector3(maxX, minY, bounds.center.z));
        Vector3 topRight = transform.TransformPoint(new Vector3(maxX, maxY, bounds.center.z));

        worldBounds = new Bounds(bottomLeft, Vector3.zero);
        worldBounds.Encapsulate(topLeft);
        worldBounds.Encapsulate(bottomRight);
        worldBounds.Encapsulate(topRight);
        return true;
    }

    /// <summary>
    /// Checks whether a rectangular footprint fits in the grid.
    /// The bottom-left cell is included in the footprint.
    /// </summary>
    public bool IsFootprintInsideGrid(Vector2Int bottomLeftCell, Vector2Int footprintSize)
    {
        return footprintSize.x > 0 && footprintSize.y > 0 &&
               bottomLeftCell.x >= 0 && bottomLeftCell.y >= 0 &&
               bottomLeftCell.x + footprintSize.x <= columns &&
               bottomLeftCell.y + footprintSize.y <= rows;
    }

    /// <summary>
    /// Gets the world-space center of a rectangular grid footprint.
    /// </summary>
    public bool TryGetFootprintCenterWorld(
        Vector2Int bottomLeftCell,
        Vector2Int footprintSize,
        out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!IsFootprintInsideGrid(bottomLeftCell, footprintSize) ||
            !TryGetLocalSpriteBounds(out Bounds bounds))
        {
            return false;
        }

        float cellWidth = bounds.size.x / columns;
        float cellHeight = bounds.size.y / rows;
        Vector3 localCenter = new Vector3(
            bounds.min.x + (bottomLeftCell.x + footprintSize.x * 0.5f) * cellWidth,
            bounds.min.y + (bottomLeftCell.y + footprintSize.y * 0.5f) * cellHeight,
            bounds.center.z);

        worldPosition = transform.TransformPoint(localCenter);
        return true;
    }

    /// <summary>
    /// Checks whether a zero-based cell coordinate is inside the grid.
    /// </summary>
    public bool IsCellInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < columns &&
               cell.y >= 0 && cell.y < rows;
    }

    private bool TryGetLocalSpriteBounds(out Bounds bounds)
    {
        CacheSpriteRenderer();

        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            bounds = default;
            return false;
        }

        // Sprite.bounds is expressed in the SpriteRenderer object's local space.
        bounds = spriteRenderer.sprite.bounds;
        return bounds.size.x > 0f && bounds.size.y > 0f;
    }

    private void CacheSpriteRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void OnDrawGizmos()
    {
        if (!TryGetLocalSpriteBounds(out Bounds bounds))
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;

        for (int column = 0; column <= columns; column++)
        {
            float x = bounds.min.x + bounds.size.x * column / columns;
            Gizmos.DrawLine(
                new Vector3(x, bounds.min.y, bounds.center.z),
                new Vector3(x, bounds.max.y, bounds.center.z));
        }

        for (int row = 0; row <= rows; row++)
        {
            float y = bounds.min.y + bounds.size.y * row / rows;
            Gizmos.DrawLine(
                new Vector3(bounds.min.x, y, bounds.center.z),
                new Vector3(bounds.max.x, y, bounds.center.z));
        }

        Gizmos.matrix = previousMatrix;
    }
}
