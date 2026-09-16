using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores completed result tokens in a simple row-based settlement layout.
/// </summary>
public class SettlementArea : MonoBehaviour
{
    [SerializeField, Min(1)] private int columns = 5;
    [SerializeField] private Vector2 spacing = new Vector2(1f, 1f);
    [SerializeField] private Vector2 firstTokenLocalPosition = Vector2.zero;
    [SerializeField] private Collider2D areaCollider;
    [SerializeField] private Camera inputCamera;

    private readonly List<NumberToken> placedNumbers = new List<NumberToken>();

    private void Awake()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<Collider2D>();
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    private void Start()
    {
        if (areaCollider == null)
        {
            Debug.LogWarning($"{name}：结算区没有 Collider2D，请指定 Area Collider。", this);
        }
    }

    /// <summary>
    /// Adds drag behavior to a newly-created press result so the player can
    /// deliver it by dropping it over this area's collider.
    /// </summary>
    public void PrepareForDelivery(NumberToken token)
    {
        if (token == null)
        {
            return;
        }

        NumberTokenDrag drag = token.GetComponent<NumberTokenDrag>();
        if (drag == null)
        {
            drag = token.gameObject.AddComponent<NumberTokenDrag>();
        }

        drag.Configure(inputCamera, null, this);
    }

    public bool ContainsPoint(Vector2 worldPosition)
    {
        return areaCollider != null && areaCollider.OverlapPoint(worldPosition);
    }

    public void PlaceNumber(NumberToken token)
    {
        if (token == null)
        {
            return;
        }

        placedNumbers.RemoveAll(number => number == null);
        if (placedNumbers.Contains(token))
        {
            return;
        }

        int index = placedNumbers.Count;
        int safeColumns = Mathf.Max(1, columns);
        Vector3 localPosition = new Vector3(
            firstTokenLocalPosition.x + (index % safeColumns) * spacing.x,
            firstTokenLocalPosition.y - (index / safeColumns) * spacing.y,
            0f);

        token.transform.SetParent(transform, true);
        token.transform.position = transform.TransformPoint(localPosition);
        placedNumbers.Add(token);
        Debug.Log($"结果数字 {token.Value} 已放入结算区。", token);
    }

    public void ClearNumbers()
    {
        foreach (NumberToken token in placedNumbers)
        {
            if (token != null)
            {
                Destroy(token.gameObject);
            }
        }

        placedNumbers.Clear();
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        spacing.x = Mathf.Max(0.001f, spacing.x);
        spacing.y = Mathf.Max(0.001f, spacing.y);
    }
}
