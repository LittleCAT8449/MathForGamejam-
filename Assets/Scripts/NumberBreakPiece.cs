using UnityEngine;

/// <summary>
/// Fades and removes one visual fragment created by NumberBreakEffect. The
/// fragment has no collider, so it cannot block the press while it bursts out.
/// </summary>
public class NumberBreakPiece : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Mesh fragmentMesh;
    private Material fragmentMaterial;
    private Color startColor;
    private float lifetime;
    private float elapsed;

    public void Initialize(
        MeshRenderer targetRenderer,
        Color color,
        float duration,
        Mesh runtimeMesh,
        Material runtimeMaterial)
    {
        meshRenderer = targetRenderer;
        fragmentMesh = runtimeMesh;
        fragmentMaterial = runtimeMaterial;
        startColor = color;
        lifetime = Mathf.Max(0.01f, duration);
        elapsed = 0f;
        ApplyColor(startColor);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / lifetime);
        Color color = startColor;
        color.a *= 1f - progress;
        ApplyColor(color);

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (fragmentMesh != null)
        {
            Destroy(fragmentMesh);
        }

        if (fragmentMaterial != null)
        {
            Destroy(fragmentMaterial);
        }
    }

    private void ApplyColor(Color color)
    {
        if (fragmentMaterial == null)
        {
            return;
        }

        if (fragmentMaterial.HasProperty("_Color"))
        {
            fragmentMaterial.SetColor("_Color", color);
        }

        if (fragmentMaterial.HasProperty("_BaseColor"))
        {
            fragmentMaterial.SetColor("_BaseColor", color);
        }
    }
}
