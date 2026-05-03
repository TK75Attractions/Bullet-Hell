using Unity.Mathematics;

namespace BulletHell.Bullets
{
public struct LASERvertex
{
    public float2 point;
    public float2 neutral;
    public float magnitude 
    {
        get 
        {
            if (neutral.x == 0 && neutral.y == 0) return 1;
            return math.sqrt(neutral.x * neutral.x + neutral.y * neutral.y); 
        }
    }// |(f'(x), -1)|

    public LASERvertex(float2 _p, float2 _n)
    {
        point = _p;
        neutral = _n;
    }
}
}