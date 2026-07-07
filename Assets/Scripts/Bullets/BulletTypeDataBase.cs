using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Bullet/BulletTypeDataBase", fileName = "BulletTypeDataBase")]
public class BulletTypeDataBase : ScriptableObject
{
    public BulletType[] types = new BulletType[0];
    public List<float2[]> bVerts = new List<float2[]>();
    public List<float> bPower = new List<float>();

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
            else
            {
                types[i].Init();
            }
            if (max < i) max = i;
        }
        BulletType[] temp = new BulletType[max + 1];
        List<float2[]> tempVerts = new List<float2[]>(max + 1);
        List<float> tempPower = new List<float>(max + 1);
        for (int i = 0; i < max + 1; i++) tempVerts.Add(null);
        for (int i = 0; i < max + 1; i++) tempPower.Add(0f);
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null)
            {
                continue;
            }

            int k = i; // ここで i を k に代入しておくことで、後のコードで i を変更しても問題なくアクセスできるようにする
            if (k < 0)
            {
                Debug.LogWarning($"BulletType at index {i} has a negative typeId ({k}). This may cause issues.");
                continue;
            }
            temp[k] = types[i];
            tempVerts[k] = types[i].verts;
            tempPower[k] = types[i].counterPower;
        }
        types = temp;
        bVerts = tempVerts;
        bPower = tempPower;

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

    public int GetTypeId(string typeName)
    {
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] != null && types[i].typeName == typeName) return i;
        }
        Debug.LogWarning($"BulletType with name '{typeName}' not found! Returning -1.");
        return -1;
    }
}
