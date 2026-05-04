using Unity.Mathematics;
using System.Collections.Generic;

namespace BulletHell.Bullets
{
    public interface IBulletPaternProvider
    {
        void Init();
        bool TryGetBulletClipIndex(string name, out int index);
        List<BulletData> GetBulletClip(int index, float2 pPos, float2 _vlc, float angle, out bool isLaser);
    }
}