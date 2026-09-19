using UnityEngine;
using TMPro;
using System.Collections;
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
    public bool IsConsumed { get; private set; }
    public static NumberToken SelectedToken { get; private set; }

    /// <summary>
    /// The background sprite used by the break effect.
    /// </summary>
    public SpriteRenderer VisualRenderer
    {
        get
        {
            CacheComponents();
            return spriteRenderer;
        }
    }

    /// <summary>
    /// Keeps this number object alive after it is consumed. The background
    /// square fades out while the TMP value remains visible, and the token is
    /// made non-interactive so it cannot be consumed or dragged again.
    /// </summary>
    public void MarkConsumedAndFade(float duration)
    {
        CacheComponents();
        LockAfterConsumption();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeSpriteToTransparent(duration));
    }

    /// <summary>
    /// Locks a token for the stamping convergence animation without fading or
    /// destroying it. The caller can move the whole token, including its TMP
    /// label, and destroy it after the group has converged.
    /// </summary>
    public void MarkConsumedForConvergence()
    {
        CacheComponents();
        LockAfterConsumption();
    }

    /// <summary>
    /// Locks a token and makes only its SpriteRenderer fully transparent
    /// immediately. The TMP value remains active until the caller destroys
    /// the token after the convergence animation.
    /// </summary>
    public void MarkConsumedAndHide()
    {
        CacheComponents();
        LockAfterConsumption();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 0f;
            spriteRenderer.color = color;
        }
    }

    /// <summary>
    /// Moves the complete number object while it is converging with the other
    /// numbers.
    /// </summary>
    public void SetConvergencePosition(Vector3 worldPosition)
    {
        worldPosition.z = transform.position.z;
        transform.position = worldPosition;
    }

    private SpriteRenderer spriteRenderer;
    private Color originalSpriteColor;
    private bool originalSpriteColorCached;
    private Coroutine fadeCoroutine;

    private void LockAfterConsumption()
    {
        IsConsumed = true;
        SetSelected(false);

        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        NumberTokenDrag drag = GetComponent<NumberTokenDrag>();
        if (drag != null)
        {
            drag.enabled = false;
        }

        // A token dropped into the stamping area may already have a dynamic
        // Rigidbody2D. Freeze it so the TMP label does not fall during the
        // convergence animation.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private IEnumerator FadeSpriteToTransparent(float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                spriteRenderer.color = color;
            }

            yield return null;
        }

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 0f;
            spriteRenderer.color = color;
        }

        fadeCoroutine = null;
    }

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
