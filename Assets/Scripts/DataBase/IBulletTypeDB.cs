using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
namespace BulletHell.Bullets
{
    public interface IBulletTypeDB
    {
        public void Init();

        public BulletType[] GetTypes();

        public List<float2[]> bVerts { get; }

        public Texture2D[] GetBaseTextures();
        public Texture2D[] GetMaskTextures();
    }
}