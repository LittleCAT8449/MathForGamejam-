#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CustomTilemapShape))]
public class CustomTilemapShapeEditor : Editor
{
    private SerializedProperty tilemapProperty;
    private SerializedProperty tileProperty;
    private SerializedProperty shapeDataProperty;
    private SerializedProperty originProperty;
    private SerializedProperty generateOnAwakeProperty;

    private void OnEnable()
    {
        tilemapProperty = serializedObject.FindProperty("tilemap");
        tileProperty = serializedObject.FindProperty("tile");
        shapeDataProperty = serializedObject.FindProperty("shapeData");
        originProperty = serializedObject.FindProperty("tilemapOrigin");
        generateOnAwakeProperty = serializedObject.FindProperty("generateOnAwake");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(tilemapProperty);
        EditorGUILayout.PropertyField(tileProperty);
        EditorGUILayout.PropertyField(shapeDataProperty);
        EditorGUILayout.PropertyField(originProperty);
        EditorGUILayout.PropertyField(generateOnAwakeProperty);

        EditorGUILayout.Space(6f);
        if (GUILayout.Button("生成 Tile"))
        {
            serializedObject.ApplyModifiedProperties();
            ((CustomTilemapShape)target).Generate();
            EditorUtility.SetDirty(target);
        }

        if (GUILayout.Button("清除 Tile"))
        {
            ((CustomTilemapShape)target).ClearShape();
            EditorUtility.SetDirty(target);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
