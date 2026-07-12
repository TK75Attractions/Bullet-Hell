using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "BulletType", menuName = "Bullet/BulletType")]
public class BulletType : ScriptableObject
{
    public string typeName;

    public struct TextureBuffer
    {
        public Texture2D baseSprite;
        public Texture2D maskSprite;
        public float time;
    }

    [Header("Rendering")]
    public Texture2D baseSprite;
    public Texture2D maskSprite;
    public Color baseColor = Color.white;
    public float baseSize;
    public int renderPriority;
    public float counterPower;
    // 描画角を常に0(正立)に固定する。公転(polarForm.y)や速度角に追従させたくない
    // キャラ系スプライト弾(幽霊リング等)用。当たり判定の角度には影響しない。
    public bool uprightSprite;

    [Header("Collider")]
    public float2[] verts = new float2[0];

    public void Init()
    {
        if (verts == null || verts.Length < 3)
        {
            counterPower = 0f;
            return;
        }

        // Shoelace formula for polygon area from collider vertices.
        float signedDoubleArea = 0f;
        for (int i = 0; i < verts.Length; i++)
        {
            float2 current = verts[i];
            float2 next = verts[(i + 1) % verts.Length];
            signedDoubleArea += current.x * next.y - next.x * current.y;
        }

        counterPower = math.abs(signedDoubleArea) * 0.5f;
    }
}
