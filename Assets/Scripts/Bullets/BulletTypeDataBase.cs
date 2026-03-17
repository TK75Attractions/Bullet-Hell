using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Bullet/BulletTypeDataBase", fileName = "BulletTypeDataBase")]
public class BulletTypeDataBase : ScriptableObject
{
    public BulletType[] types = new BulletType[0];
    public List<float2[]> bVerts = new List<float2[]>();

    public void Init()
    {
        int max = 0;

        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null)
            {
                Debug.LogWarning($"BulletType at index {i} is null! This may cause issues.");
                continue;
            }
            if (max < types[i].typeId) max = types[i].typeId;
        }
        BulletType[] temp = new BulletType[max + 1];

        List<float2[]> tempVerts = new List<float2[]>(max + 1);
        for (int i = 0; i < max + 1; i++) tempVerts.Add(null);
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null)
            {
                continue;
            }

            int k = types[i].typeId;
            if (k < 0)
            {
                Debug.LogWarning($"BulletType at index {i} has a negative typeId ({k}). This may cause issues.");
                continue;
            }
            temp[k] = types[i];
            tempVerts[k] = types[i].verts;
        }
        types = temp;
        bVerts = tempVerts;

        if (GManager.Control != null && GManager.Control.QOrder != null)
        {
            GManager.Control.QOrder.MarkCollisionDataDirty();
        }
    }

    public Texture2D[] GetBaseTextures()
    {
        Texture2D[] textures = new Texture2D[types.Length];
        for (int i = 0; i < types.Length; i++) textures[i] = types[i].baseSprite;
        return textures;
    }

    public Texture2D[] GetMaskTextures()
    {
        Texture2D[] textures = new Texture2D[types.Length];
        for (int i = 0; i < types.Length; i++) textures[i] = types[i].maskSprite;
        return textures;
    }
}
