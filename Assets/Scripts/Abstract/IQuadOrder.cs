using Unity.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

using BulletHell.Enemies;

namespace BulletHell.Bullets
{
    public interface IQuadOrder
    {

    public void AwakeSetting();

    public void MarkCollisionDataDirty();
    public void QuadUpdate(float _dt);

    public List<int> AddEnemyHomingBullets(NativeArray<BulletData> newBullets, float2 fromPos);
    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos = new float2());
    public void AddEnemyBullets(BulletSpawner spawner);

    public List<int> EmitEnemyBullet(BulletClip clip, int EnemyIndex);

    public List<int> EmitEnemyBullet(BulletClip clip, float2 pPos);

    public void StartBulletEvent(BulletEvent bulletEvent);
    public List<int> UpdateBulletData(List<int> indexes, BulletClip clip);
    }
}
