using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

[CreateAssetMenu(fileName ="BulletType",menuName ="Bullet/BulletType")]
public class BulletType : ScriptableObject
{
    public int typeId;

    [Header("Rendering")]
    public Texture2D baseSprite;
    public Texture2D maskSprite;
    public Color baseColor = Color.white;
    public float baseSize;

    
    [Header("Collider")]
    public float2[] verts = new float2[0];
}
