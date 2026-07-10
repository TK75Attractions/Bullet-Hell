using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Redraws the player's sprite on top of the GPU bullet layer so the player
/// stays readable when shards/cutters overlap it.
///
/// URP's 2D renderer draws SRPDefaultUnlit geometry (the instanced bullets in
/// <see cref="BulletRenderSystem"/>) after the 2D sprite passes, so a plain
/// <see cref="SpriteRenderer"/> is always behind the bullets. This component
/// re-issues the player's sprite into that same unlit collection via
/// <see cref="Graphics.DrawMesh"/> using a material at Queue = Transparent+100,
/// which sorts after the bullet material (Transparent) and keeps the player on
/// top. Additive and non-destructive: the original SpriteRenderer is untouched.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFrontOverlay : MonoBehaviour
{
    private SpriteRenderer source;
    private Material overlayMat;
    private MaterialPropertyBlock mpb;
    private Mesh quad;
    private Sprite builtFrom;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int Color1Id = Shader.PropertyToID("_Color1");
    private static readonly int Color2Id = Shader.PropertyToID("_Color2");

    private void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        Shader sh = Shader.Find("Custom/PlayerFrontOverlay");
        if (sh != null)
        {
            overlayMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (source == null || overlayMat == null || !source.enabled) return;
        Sprite sprite = source.sprite;
        if (sprite == null) return;

        RebuildIfNeeded(sprite);
        if (quad == null) return;

        mpb.SetTexture(MainTexId, sprite.texture);
        mpb.SetColor(ColorId, source.color);
        Material sourceMaterial = source.sharedMaterial;
        mpb.SetColor(Color1Id, sourceMaterial != null && sourceMaterial.HasProperty(Color1Id)
            ? sourceMaterial.GetColor(Color1Id)
            : PlayerPaletteDefaults.Color1Linear);
        mpb.SetColor(Color2Id, sourceMaterial != null && sourceMaterial.HasProperty(Color2Id)
            ? sourceMaterial.GetColor(Color2Id)
            : PlayerPaletteDefaults.Color2Linear);
        Graphics.DrawMesh(quad, transform.localToWorldMatrix, overlayMat, gameObject.layer, null, 0, mpb);
    }

    private void RebuildIfNeeded(Sprite sprite)
    {
        if (sprite == builtFrom && quad != null) return;
        builtFrom = sprite;

        if (quad == null) quad = new Mesh { name = "PlayerFrontOverlayQuad" };
        quad.Clear();

        Vector2[] srcVerts = sprite.vertices;
        Vector3[] verts = new Vector3[srcVerts.Length];
        for (int i = 0; i < srcVerts.Length; i++)
        {
            verts[i] = new Vector3(srcVerts[i].x, srcVerts[i].y, 0f);
        }

        ushort[] srcTris = sprite.triangles;
        int[] tris = new int[srcTris.Length];
        for (int i = 0; i < srcTris.Length; i++)
        {
            tris[i] = srcTris[i];
        }

        quad.SetVertices(new List<Vector3>(verts));
        quad.SetUVs(0, new List<Vector2>(sprite.uv));
        quad.SetTriangles(tris, 0);
    }

    private void OnDestroy()
    {
        if (overlayMat != null) Destroy(overlayMat);
        if (quad != null) Destroy(quad);
    }
}
