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
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    public decimal Value { get; private set; }
    public bool IsSelected { get; private set; }
    public static NumberToken SelectedToken { get; private set; }

    private SpriteRenderer spriteRenderer;
    private Color originalSpriteColor;
    private bool originalSpriteColorCached;

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

            if (spriteRenderer != null && !originalSpriteColorCached)
            {
                originalSpriteColor = spriteRenderer.color;
                originalSpriteColorCached = true;
            }
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

    /// <summary>
    /// Selects this number and clears the previous number selection.
    /// </summary>
    public void SetSelected(bool selected)
    {
        CacheComponents();

        if (selected)
        {
            if (SelectedToken != null && SelectedToken != this)
            {
                SelectedToken.SetSelected(false);
            }

            SelectedToken = this;
        }
        else if (SelectedToken == this)
        {
            SelectedToken = null;
        }

        IsSelected = selected;
        if (spriteRenderer != null && originalSpriteColorCached)
        {
            spriteRenderer.color = selected
                ? selectedColor
                : originalSpriteColor;
        }
    }

    public void ToggleSelected()
    {
        SetSelected(!IsSelected);
        Debug.Log(
            IsSelected ? $"选中数字：{Value}" : $"取消选择数字：{Value}",
            this);
    }

    private void OnDestroy()
    {
        if (SelectedToken == this)
        {
            SelectedToken = null;
        }
    }
}
