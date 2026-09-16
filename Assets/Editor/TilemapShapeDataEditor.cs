#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TilemapShapeData))]
public class TilemapShapeDataEditor : Editor
{
    private SerializedProperty shapeSizeProperty;
    private SerializedProperty filledCellsProperty;

    private void OnEnable()
    {
        shapeSizeProperty = serializedObject.FindProperty("shapeSize");
        filledCellsProperty = serializedObject.FindProperty("filledCells");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(shapeSizeProperty);
        Vector2Int size = shapeSizeProperty.vector2IntValue;
        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);
        shapeSizeProperty.vector2IntValue = size;

        HashSet<Vector2Int> selected = ReadSelectedCells();
        HashSet<Vector2Int> edited = new HashSet<Vector2Int>(selected);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("形状网格（点击格子切换）", EditorStyles.boldLabel);

        for (int y = size.y - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool wasSelected = edited.Contains(cell);
                bool isSelected = GUILayout.Toggle(
                    wasSelected,
                    wasSelected ? "■" : "·",
                    GUI.skin.button,
                    GUILayout.Width(28f),
                    GUILayout.Height(28f));

                if (isSelected != wasSelected)
                {
                    if (isSelected) edited.Add(cell);
                    else edited.Remove(cell);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        if (!selected.SetEquals(edited))
        {
            WriteSelectedCells(edited);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全部填充"))
        {
            HashSet<Vector2Int> all = new HashSet<Vector2Int>();
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    all.Add(new Vector2Int(x, y));
                }
            }

            WriteSelectedCells(all);
        }

        if (GUILayout.Button("清空形状"))
        {
            WriteSelectedCells(new HashSet<Vector2Int>());
        }

        EditorGUILayout.EndHorizontal();
        serializedObject.ApplyModifiedProperties();
    }

    private HashSet<Vector2Int> ReadSelectedCells()
    {
        HashSet<Vector2Int> selected = new HashSet<Vector2Int>();
        for (int i = 0; i < filledCellsProperty.arraySize; i++)
        {
            selected.Add(filledCellsProperty.GetArrayElementAtIndex(i).vector2IntValue);
        }

        return selected;
    }

    private void WriteSelectedCells(HashSet<Vector2Int> cells)
    {
        List<Vector2Int> ordered = new List<Vector2Int>(cells);
        ordered.Sort((left, right) =>
        {
            int yCompare = left.y.CompareTo(right.y);
            return yCompare != 0 ? yCompare : left.x.CompareTo(right.x);
        });

        filledCellsProperty.arraySize = ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
        {
            filledCellsProperty.GetArrayElementAtIndex(i).vector2IntValue = ordered[i];
        }
    }
}
#endif
