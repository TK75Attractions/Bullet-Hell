using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using System;
using UnityEngine;

namespace BulletHell.Bullets
{
public class QuadBulletStore : IQuadBulletStore
{
    private NativeList<BulletData> _playerBullets;
    private NativeList<BulletData> _enemyBullets;
    private NativeList<BulletData> _enemiesOrbitBullets;

    public NativeArray<BulletData> playerBullets => _playerBullets.AsArray();
    public NativeArray<BulletData> enemyBullets => _enemyBullets.AsArray();
    public NativeArray<BulletData> enemiesOrbitBullets => _enemiesOrbitBullets.AsArray();

    public void Init(int capacity = 256)
    {
        if (!_enemyBullets.IsCreated)
        {
            _enemyBullets = new NativeList<BulletData>(capacity, Allocator.Persistent);
        }
        if (!_playerBullets.IsCreated)
        {
            _playerBullets = new NativeList<BulletData>(capacity, Allocator.Persistent);
        }
        if (!_enemiesOrbitBullets.IsCreated)
        {
            _enemiesOrbitBullets = new NativeList<BulletData>(capacity, Allocator.Persistent);
        }
    }

    #region GetCount Methods
    public int GetPlayerBulletCount() => GetActiveBulletCount(_playerBullets);
    public int GetEnemyBulletCount() => GetActiveBulletCount(_enemyBullets);
    public int GetEnemiesOrbitBulletCount() => GetActiveBulletCount(_enemiesOrbitBullets);

    private int GetActiveBulletCount(NativeList<BulletData> bullets)
    {
        if (!bullets.IsCreated) return 0;

        int count = 0;
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].isActive) count++;
        }
        return count;
    }
    #endregion

    #region Bullet Return Methods
    public NativeArray<BulletData> GetPlayerBullets() => playerBullets.IsCreated ? playerBullets : default;
    public NativeArray<BulletData> GetEnemyBullets() => enemyBullets.IsCreated ? enemyBullets : default;
    #endregion

    #region Bullet Return Methods
    public BulletData GetEnemyBulletData(int index)
    {
        if (!_enemyBullets.IsCreated || index < 0 || index >= _enemyBullets.Length)
        {
            throw new IndexOutOfRangeException($"Bullet index {index} is out of range.");
        }
        return _enemyBullets[index];
    }
    #endregion

    #region Bullets Addition Methods
    public void AddPlayerBullets(NativeArray<BulletData> newBullets)
    {
        if (newBullets.Length == 0) return;

        if (!_playerBullets.IsCreated)
        {
            _playerBullets = new NativeList<BulletData>(math.max(256, newBullets.Length), Allocator.Persistent);
        }

        int oldLength = _playerBullets.Length;
        int newLength = oldLength + newBullets.Length;

        if (_playerBullets.Capacity < newLength)
        {
            int nextCapacity = math.max(newLength, math.max(256, _playerBullets.Capacity * 2));
            _playerBullets.Capacity = nextCapacity;
        }

        _playerBullets.ResizeUninitialized(newLength);

        // 新しい弾をコピー
        for (int i = 0; i < newBullets.Length; i++)
        {
            _playerBullets[oldLength + i] = newBullets[i];
        }
    }

    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos = new float2())
    {
        Debug.Log($"Length: {newBullets.Length}");

        if (newBullets.Length == 0) return null;

        if (!_enemyBullets.IsCreated)
        {
            _enemyBullets = new NativeList<BulletData>(math.max(256, newBullets.Length), Allocator.Persistent);
        }

        int oldLength = _enemyBullets.Length;
        int newLength = oldLength + newBullets.Length;

        if (_enemyBullets.Capacity < newLength)
        {
            int nextCapacity = math.max(newLength, math.max(256, _enemyBullets.Capacity * 2));
            _enemyBullets.Capacity = nextCapacity;
        }

        _enemyBullets.ResizeUninitialized(newLength);
        List<int> indexes = new List<int>(newBullets.Length);

        // 新しい弾をコピー
        for (int i = 0; i < newBullets.Length; i++)
        {
            _enemyBullets[oldLength + i] = newBullets[i];
            indexes.Add(oldLength + i);
        }
        return indexes;
    }

    public List<int> AddEnemyHomingBullets(NativeArray<BulletData> newBullets, float2 fromPos, float2 playerPos)
    {
        NativeArray<BulletData> tempBullets = newBullets;
        float2 toPlayer = playerPos - fromPos;
        float angleToPlayer = math.atan2(toPlayer.y, toPlayer.x);

        for (int i = 0; i < newBullets.Length; i++)
        {
            BulletData bullet = newBullets[i];
            bullet.Init(fromPos);
            bullet.originPos = fromPos;
            bullet.polarForm = new float2(bullet.polarForm.x, bullet.polarForm.y + angleToPlayer);
            tempBullets[i] = bullet;
        }

        List<int> indexes = AddEnemyBullets(tempBullets);
        return indexes;
    }

    public void AddEnemiesOrbitBullet(BulletData newBullet)
    {
        if (!_enemiesOrbitBullets.IsCreated)
        {
            _enemiesOrbitBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }

        _enemiesOrbitBullets.Add(newBullet);
    }
    

    public void AddEnemyBulletsBySpawner(NativeArray<BulletData> newBullets)
    {
        AddEnemyBullets(newBullets);
    }
    #endregion

    public List<int> EmitEnemyBullet(BulletClip clip, float2 pPos, float2 playerPos)
    {
         NativeArray<BulletData> newBullets = new NativeArray<BulletData>(clip.number, Allocator.Temp);

        float2 dis = new float2(playerPos) - pPos;
        float range = (clip.number - 1) * clip.disRad;

        if (clip.homing)
        {
            float baseAngle = math.atan2(dis.y, dis.x);

            for (int i = 0; i < clip.number; i++)
            {
                BulletData bullet = clip.data;
                bullet.Init(pPos);
                float angle = baseAngle + math.radians(-range / 2 + clip.disRad * i);
                bullet.polarForm = new float2(bullet.polarForm.x, angle);
                newBullets[i] = bullet;
            }
        }
        else
        {
            for (int i = 0; i < clip.number; i++)
            {
                BulletData bullet = clip.data;
                bullet.Init(pPos);
                float angle = math.radians(range * i);
                bullet.polarForm = new float2(bullet.polarForm.x, angle);
                newBullets[i] = bullet;
            }
        }

        Debug.Log(newBullets);
        List<int> indexes = AddEnemyBullets(newBullets);
        Debug.Log($"indexs: {indexes}");

        newBullets.Dispose();
        return indexes;
    }

    
    public List<int> UpdateBulletData(List<int> indexes, BulletClip clip, float2 playerPos)
    {
        if (indexes == null || indexes.Count == 0) return new();

        if (indexes.Count == 1 && clip.number == 1)
        {
            if (indexes[0] >= 0 && indexes[0] < _enemyBullets.Length)
            {
                BulletData data = new BulletData(clip.data, new float2(0, 0), _enemyBullets[indexes[0]].position, 0);
                _enemyBullets[indexes[0]] = data;
                return new List<int>() { indexes[0] };
            }
        }
        else
        {
            List<int> temp = new();
            for (int i = 0; i < indexes.Count; i++)
            {
                int index = indexes[i];
                if (index < 0 || index >= _enemyBullets.Length) continue;


                var tmp = EmitEnemyBullet(clip, _enemyBullets[index].position, playerPos);

                temp.AddRange(tmp);
                BulletData data = _enemyBullets[index];
                data.isActive = false;
                _enemyBullets[index] = data;
            }
            return temp;

        }
        return new();
    }

    public (float2 position, float angle) GetEnemyOrbitsBulletData(int index)
    {
        if (!_enemiesOrbitBullets.IsCreated || index < 0 || index >= _enemiesOrbitBullets.Length)
        {
            throw new IndexOutOfRangeException($"Bullet index {index} is out of range.");
        }
        return (_enemiesOrbitBullets[index].position, _enemiesOrbitBullets[index].angle);
    }

    public void Dispose()
    {
        if (_playerBullets.IsCreated) _playerBullets.Dispose();
        if (_enemyBullets.IsCreated) _enemyBullets.Dispose();
        if (_enemiesOrbitBullets.IsCreated) _enemiesOrbitBullets.Dispose();
    }
}
}