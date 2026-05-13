using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName;

    [Header("Rendering")]
    public Sprite sprite;
    public Color baseColor = Color.white;
    public float baseSize;
}
