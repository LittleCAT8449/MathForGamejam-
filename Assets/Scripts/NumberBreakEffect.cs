using UnityEngine;

/// <summary>
/// Creates lightweight visual MeshRenderer fragments from a NumberToken.
/// The original square is faded out while its TMP label remains visible.
/// </summary>
public class NumberBreakEffect : MonoBehaviour
{
    [SerializeField, Min(1)] private int columns = 3;
    [SerializeField, Min(1)] private int rows = 3;
    [SerializeField, Min(0.01f)] private float lifetime = 0.45f;
    [SerializeField, Min(0f)] private float burstSpeed = 1.8f;
    [SerializeField] private float upwardBias = 0.35f;
    [SerializeField, Min(0f)] private float gravityScale = 0.8f;
    [SerializeField, Min(0f)] private float randomRotationSpeed = 360f;

    /// <summary>
    /// Fades the token square, keeps its TMP label, then creates lightweight
/// visual fragments that burst outward from the number.
    /// </summary>
    public void Play(NumberToken token)
    {
        Play(token, lifetime);
    }

    /// <summary>
    /// Plays the break effect using a caller-provided duration. The stamping
    /// machine uses its convergence duration so the square reaches zero
    /// opacity before the source token is destroyed.
    /// </summary>
    public void Play(NumberToken token, float durationOverride)
    {
        if (token == null)
        {
            return;
        }

        float effectDuration = durationOverride > 0f
            ? durationOverride
            : lifetime;

        SpriteRenderer sourceRenderer = token.VisualRenderer;
        Sprite sourceSprite = sourceRenderer != null ? sourceRenderer.sprite : null;
        Color fragmentColor = sourceRenderer != null
            ? sourceRenderer.color
            : Color.white;

        // Hide only the original square immediately. Keep the captured color
        // for the fragments so the break animation remains visible.
        token.MarkConsumedAndHide();

        if (sourceRenderer == null || sourceSprite == null)
        {
            return;
        }

        Rect textureRect = sourceSprite.textureRect;
        Texture2D texture = sourceSprite.texture;
        if (texture == null || texture.width <= 0 || texture.height <= 0)
        {
            return;
        }

        Vector3 tokenPosition = token.transform.position;
        Vector3 tokenScale = token.transform.lossyScale;
        Quaternion tokenRotation = token.transform.rotation;
        Bounds spriteBounds = sourceSprite.bounds;
        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        for (int x = 0; x < safeColumns; x++)
        {
            for (int y = 0; y < safeRows; y++)
            {
                CreatePiece(
                    sourceRenderer,
                    sourceSprite,
                    textureRect,
                    texture,
                    spriteBounds,
                    tokenPosition,
                    tokenScale,
                    tokenRotation,
                    x,
                    y,
                    safeColumns,
                    safeRows,
                    effectDuration,
                    fragmentColor);
            }
        }
    }

    private void CreatePiece(
        SpriteRenderer sourceRenderer,
        Sprite sourceSprite,
        Rect textureRect,
        Texture2D texture,
        Bounds spriteBounds,
        Vector3 tokenPosition,
        Vector3 tokenScale,
        Quaternion tokenRotation,
        int x,
        int y,
        int pieceColumns,
        int pieceRows,
        float effectDuration,
        Color fragmentColor)
    {
        float x0 = Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, (float)x / pieceColumns);
        float x1 = Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, (float)(x + 1) / pieceColumns);
        float y0 = Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, (float)y / pieceRows);
        float y1 = Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, (float)(y + 1) / pieceRows);

        float u0 = (textureRect.xMin + textureRect.width * x / pieceColumns) / texture.width;
        float u1 = (textureRect.xMin + textureRect.width * (x + 1) / pieceColumns) / texture.width;
        float v0 = (textureRect.yMin + textureRect.height * y / pieceRows) / texture.height;
        float v1 = (textureRect.yMin + textureRect.height * (y + 1) / pieceRows) / texture.height;

        GameObject piece = new GameObject($"NumberBreakPiece_{x}_{y}");
        Vector2 localCenter = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        Vector3 scaledCenter = Vector3.Scale(localCenter, tokenScale);
        Vector2 worldCenter = (Vector2)(tokenPosition + tokenRotation * scaledCenter);
        piece.transform.SetPositionAndRotation(worldCenter, tokenRotation);
        piece.transform.localScale = tokenScale;

        Mesh pieceMesh = CreatePieceMesh(x0, x1, y0, y1, u0, u1, v0, v1);
        MeshFilter meshFilter = piece.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = pieceMesh;

        Material pieceMaterial = CreateFragmentMaterial(sourceRenderer.sharedMaterial, texture);
        MeshRenderer fragmentRenderer = piece.AddComponent<MeshRenderer>();
        fragmentRenderer.sharedMaterial = pieceMaterial;
        fragmentRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        fragmentRenderer.sortingOrder = sourceRenderer.sortingOrder;

        NumberBreakPiece breakPiece = piece.AddComponent<NumberBreakPiece>();
        breakPiece.Initialize(
            fragmentRenderer,
            fragmentColor,
            effectDuration,
            pieceMesh,
            pieceMaterial);

        // Restore the original outward burst: fragments are launched away
        // from the number, receive a small upward bias, and fall naturally.
        Rigidbody2D body = piece.AddComponent<Rigidbody2D>();
        body.gravityScale = gravityScale;
        body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        Vector2 direction = worldCenter - (Vector2)tokenPosition;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Random.insideUnitCircle;
        }

        direction = (direction.normalized + Vector2.up * upwardBias).normalized;
        body.linearVelocity = direction * burstSpeed * Random.Range(0.75f, 1.25f);
        body.angularVelocity = Random.Range(-randomRotationSpeed, randomRotationSpeed);
    }

    private static Mesh CreatePieceMesh(
        float x0,
        float x1,
        float y0,
        float y1,
        float u0,
        float u1,
        float v0,
        float v1)
    {
        Mesh mesh = new Mesh
        {
            name = "NumberBreakPieceMesh",
            vertices = new[]
            {
                new Vector3(x0, y0, 0f),
                new Vector3(x1, y0, 0f),
                new Vector3(x1, y1, 0f),
                new Vector3(x0, y1, 0f)
            },
            colors = new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            },
            uv = new[]
            {
                new Vector2(u0, v0),
                new Vector2(u1, v0),
                new Vector2(u1, v1),
                new Vector2(u0, v1)
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateFragmentMaterial(Material sourceMaterial, Texture2D texture)
    {
        // Sprite-Lit-Default expects SpriteRenderer-only per-renderer data
        // such as unity_SpriteColor. These temporary MeshRenderers do not
        // provide that data, so use a regular transparent sprite shader.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        return material;
    }

}
