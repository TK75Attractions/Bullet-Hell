namespace BulletHell.Bullets
{
    public interface IBulletChangeClip
    {
        BulletClip clip { get; }
        float time { get; }
        float interval { get; }
    }
}