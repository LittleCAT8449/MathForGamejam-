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
