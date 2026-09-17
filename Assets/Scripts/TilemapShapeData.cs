using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable data asset describing which cells belong to a custom Tilemap shape.
/// </summary>
[CreateAssetMenu(fileName = "NewTilemapShape", menuName = "Mining/Tilemap Shape Data")]
public class TilemapShapeData : ScriptableObject
{
    [SerializeField] private Vector2Int shapeSize = new Vector2Int(5, 5);
    [SerializeField] private List<Vector2Int> filledCells = new List<Vector2Int>();

    public Vector2Int ShapeSize => shapeSize;
    public IReadOnlyList<Vector2Int> FilledCells => filledCells;

    /// <summary>
    /// Gets the rectangular size of the cells that are actually filled. This
    /// lets an L shape use a 2x3 footprint even when its authored canvas is
    /// larger than the visible cells.
    /// </summary>
    public bool TryGetFilledBounds(out Vector2Int minimum, out Vector2Int size)
    {
        minimum = default;
        size = Vector2Int.zero;

        if (filledCells == null || filledCells.Count == 0)
        {
            return false;
        }

        Vector2Int maximum = filledCells[0];
        minimum = maximum;

        for (int i = 1; i < filledCells.Count; i++)
        {
            Vector2Int cell = filledCells[i];
            minimum = Vector2Int.Min(minimum, cell);
            maximum = Vector2Int.Max(maximum, cell);
        }

        size = maximum - minimum + Vector2Int.one;
        return size.x > 0 && size.y > 0;
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 &&
               cell.x < Mathf.Max(1, shapeSize.x) &&
               cell.y < Mathf.Max(1, shapeSize.y);
    }

    public bool IsFilled(Vector2Int cell)
    {
        return filledCells.Contains(cell);
    }

    private void OnValidate()
    {
        shapeSize.x = Mathf.Max(1, shapeSize.x);
        shapeSize.y = Mathf.Max(1, shapeSize.y);

        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
        for (int i = filledCells.Count - 1; i >= 0; i--)
        {
            Vector2Int cell = filledCells[i];
            if (!IsInside(cell) || !uniqueCells.Add(cell))
            {
                filledCells.RemoveAt(i);
            }
        }
    }
}
