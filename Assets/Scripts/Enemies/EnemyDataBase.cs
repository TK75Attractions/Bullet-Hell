using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Enemy/EnemyDataBase", fileName = "EnemyDataBase")]
public class EnemyDataBase : ScriptableObject
{
    public List<EnemyData> enemyDataList = new List<EnemyData>();
    
    public void Init()
    {
        // Here you can add any initialization logic if needed in the future.
    }

    public Sprite GetSprite(int index)
    {
        if (index < 0 || index >= enemyDataList.Count)
        {
            Debug.LogWarning($"EnemyData at index {index} is out of range! Returning null.");
            return null;
        }
        EnemyData data = enemyDataList[index];
        if (data == null)
        {
            Debug.LogWarning($"EnemyData at index {index} is null! Returning null.");
            return null;
        }
        Sprite sprite = data.sprite;
        if (sprite == null)
        {
            Debug.LogWarning($"Base sprite for EnemyData at index {index} is null! Returning null.");
            return null;
        }
        return sprite;
    }
}
