using Unity.Mathematics;

namespace BulletHell.Bullets
{
    public interface ILASERCell
    {
        float2 vert0 { get; }
        float2 vert1 { get; }
        float2 vert2 { get; }
    }
}