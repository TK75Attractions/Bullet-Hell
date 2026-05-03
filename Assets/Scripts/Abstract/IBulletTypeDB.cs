using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
namespace BulletHell.Bullets
{
    public interface IBulletTypeDB
    {
        public void Init();

        public IBulletType[] types { get; }

        public List<float2[]> bVerts { get; }

        public Texture2D[] GetBaseTextures();
        public Texture2D[] GetMaskTextures();
    }
}