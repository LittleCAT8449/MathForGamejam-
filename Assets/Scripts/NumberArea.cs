using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the number area's top-left anchor. Spawned number prefabs are laid
/// out in rows using local positions, so the area can be moved as one object.
/// </summary>
public class NumberArea : MonoBehaviour
{
    [SerializeField] private NumberToken numberPrefab;
    [SerializeField, Min(1)] private int columns = 5;
    [SerializeField] private Vector2 spacing = new Vector2(1f, 1f);
    [SerializeField] private Vector2 firstTokenLocalPosition = Vector2.zero;
    [SerializeField] private CameraSmoothMove cameraMover;
    [SerializeField] private Vector2 cameraMoveTargetWorldPosition;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Collider2D stampingArea;

    private readonly List<NumberToken> spawnedTokens = new List<NumberToken>();

    private void Awake()
    {
        if (cameraMover == null)
        {
            cameraMover = FindFirstObjectByType<CameraSmoothMove>();
        }

        if (inputCamera == null)
        {
            inputCamera = cameraMover != null
                ? cameraMover.GetComponent<Camera>()
                : Camera.main;
        }
    }

    /// <summary>
    /// Creates and positions one number token. Returns null if no prefab is set.
    /// </summary>
    public NumberToken SpawnNumber(decimal value)
    {
        if (numberPrefab == null)
        {
            Debug.LogError($"{name}：Number Prefab 未设置，无法生成数字物体。", this);
            return null;
        }

        int index = spawnedTokens.Count;
        int safeColumns = Mathf.Max(1, columns);
        int column = index % safeColumns;
        int row = index / safeColumns;

        Vector3 localPosition = new Vector3(
            firstTokenLocalPosition.x + column * spacing.x,
            firstTokenLocalPosition.y - row * spacing.y,
            0f);
        Vector3 worldPosition = transform.TransformPoint(localPosition);

        // Instantiate unparented first, then preserve its world transform when
        // parenting. This keeps the prefab's configured world size even when
        // the number-area object has a non-unit scale.
        NumberToken token = Instantiate(numberPrefab, worldPosition, transform.rotation);
        token.transform.SetParent(transform, true);
        token.SetValue(value);

        NumberTokenDrag drag = token.GetComponent<NumberTokenDrag>();
        if (drag == null)
        {
            drag = token.gameObject.AddComponent<NumberTokenDrag>();
        }

        drag.Configure(inputCamera, stampingArea);
        spawnedTokens.Add(token);

        if (cameraMover != null)
        {
            cameraMover.MoveTo(cameraMoveTargetWorldPosition);
        }

        Debug.Log($"数字区生成数字：{value}。", token);
        return token;
    }

    /// <summary>
    /// Removes all tokens created by this area, including tokens moved elsewhere.
    /// </summary>
    public void ClearNumbers()
    {
        foreach (NumberToken token in spawnedTokens)
        {
            if (token != null)
            {
                Destroy(token.gameObject);
            }
        }

        spawnedTokens.Clear();
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        spacing.x = Mathf.Max(0.001f, spacing.x);
        spacing.y = Mathf.Max(0.001f, spacing.y);
    }
}
