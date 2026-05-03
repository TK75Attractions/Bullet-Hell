using UnityEngine;
using Unity.Mathematics;

namespace BulletHell.Bullets
{
    public interface IBulletType
    {
        public int typeId { get; }
        public Texture2D baseSprite { get; }
        public Texture2D maskSprite { get; }
        public Color baseColor { get; }
        public float baseSize { get; }

        public float2[] verts { get; }
    }
}