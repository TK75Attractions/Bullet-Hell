using UnityEngine;

namespace BulletHell.Enemies
{
    public interface IEnemyDB
    {
        public void Init();
        public Sprite GetSprite(int index);
    }
}