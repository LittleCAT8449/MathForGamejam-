using UnityEngine;
using TMPro;
using System.Globalization;

/// <summary>
/// Put this component on the number SpriteObject prefab. The value is rendered
/// with a world-space TextMeshPro component, created automatically if the prefab has none.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NumberToken : MonoBehaviour
{
    [SerializeField] private TextMeshPro valueLabel;
    [SerializeField] private Color labelColor = Color.black;

    public decimal Value { get; private set; }

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// Sets the number shown by this token.
    /// </summary>
    public void SetValue(int value)
    {
        SetValue((decimal)value);
    }

    /// <summary>
    /// Sets the number shown by this token, including fractional results.
    /// </summary>
    public void SetValue(decimal value)
    {
        CacheComponents();
        Value = value;
        valueLabel.text = value.ToString("0.############################", CultureInfo.InvariantCulture);
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (valueLabel == null)
        {
            valueLabel = GetComponentInChildren<TextMeshPro>(true);
        }

        if (valueLabel == null)
        {
            GameObject labelObject = new GameObject("NumberLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            valueLabel = labelObject.AddComponent<TextMeshPro>();
        }

        if (valueLabel.font == null && TMP_Settings.defaultFontAsset != null)
        {
            valueLabel.font = TMP_Settings.defaultFontAsset;
        }

        valueLabel.alignment = TextAlignmentOptions.Center;
        valueLabel.color = labelColor;

        MeshRenderer labelRenderer = valueLabel.GetComponent<MeshRenderer>();
        if (labelRenderer != null && spriteRenderer != null)
        {
            labelRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            labelRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
    }
}
