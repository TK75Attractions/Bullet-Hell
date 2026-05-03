namespace BulletHell.Bullets
{
    public interface IBulletClip
    {
        IBulletData data { get; }
        int number { get; }
        float disRad { get; }
        bool homing { get; }
        int generateType { get; }
    }
}