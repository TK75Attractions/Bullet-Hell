using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "BulletType", menuName = "Bullet/BulletType")]
public class BulletType : ScriptableObject
{
    public string typeName;

    [Header("Rendering")]
    public Texture2D baseSprite;
    public Texture2D maskSprite;
    public Color baseColor = Color.white;
    public float baseSize;
    public float counterPower;

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
