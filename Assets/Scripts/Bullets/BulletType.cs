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

    [Header("Collider")]
    public float2[] verts = new float2[0];

    [Header("Rendering behavior")]
    [Tooltip("If true, this type is exempt from the end-of-life disappear fade (e.g. solid stone blocks that should vanish instantly on landing/despawn).")]
    public bool skipDisappearFade;

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
